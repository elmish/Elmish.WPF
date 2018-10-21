namespace Elmish.WPF.Internal

open System
open System.Dynamic
open System.Collections.Generic
open System.Collections.ObjectModel
open System.ComponentModel
open System.Windows
open Elmish.WPF

/// Represents all necessary data used in an active binding.
type Binding<'model, 'msg> =
  | OneWay of get: ('model -> obj)
  | OneWayLazy of
      currentVal: Lazy<obj> ref
      * get: ('model -> obj)
      * map: (obj -> obj)
      * equals: (obj -> obj -> bool)
  | OneWaySeq of
      vals: ObservableCollection<obj>
      * get: ('model -> obj)
      * map: (obj -> obj seq)
      * equals: (obj -> obj -> bool)
      * getId: (obj -> obj)
      * itemEquals: (obj -> obj -> bool)
  | TwoWay of get: ('model -> obj) * set: (obj -> 'model -> 'msg)
  | TwoWayValidate of
      get: ('model -> obj)
      * set: (obj -> 'model -> 'msg)
      * validate: ('model -> Result<obj, string>)
  | TwoWayIfValid of
      get: ('model -> obj)
      * set: (obj -> 'model -> Result<'msg, string>)
  | Cmd of cmd: Command * canExec: ('model -> bool)
  | CmdIfValid of cmd: Command * exec: ('model -> Result<'msg, obj>)
  | ParamCmd of cmd: Command
  | SubModel of
      model: ViewModel<obj, obj> option ref
      * getModel: ('model -> obj option)
      * getBindings: (unit -> BindingSpec<obj, obj> list)
      * toMsg: (obj -> 'msg)
  | SubModelSeq of
      vms: ObservableCollection<ViewModel<obj, obj>>
      * getModels: ('model -> obj seq)
      * getId: (obj -> obj)
      * getBindings: (unit -> BindingSpec<obj, obj> list)
      * toMsg: (obj * obj -> 'msg)


and [<AllowNullLiteral>] ViewModel<'model, 'msg>
      ( initialModel: 'model,
        dispatch: 'msg -> unit,
        bindingSpecs: BindingSpec<'model, 'msg> list,
        config: ElmConfig,
        uiDispatch: (unit -> unit) -> unit )
      as this =
  inherit DynamicObject()

  let log fmt =
    let innerLog (str: string) =
      if config.LogConsole then Console.WriteLine(str)
      if config.LogTrace then Diagnostics.Trace.WriteLine(str)
    Printf.kprintf innerLog fmt


  let mutable currentModel = initialModel

  let propertyChanged = Event<PropertyChangedEventHandler, PropertyChangedEventArgs>()
  let errorsChanged = DelegateEvent<EventHandler<DataErrorsChangedEventArgs>>()

  /// Error messages keyed by property name.
  let errors = Dictionary<string, string>()

  let notifyPropertyChanged propName =
    log "[VM] Triggering PropertyChanged for binding %s" propName
    propertyChanged.Trigger(this, PropertyChangedEventArgs propName)

  let raiseCanExecuteChanged (cmd: Command) =
    uiDispatch cmd.RaiseCanExecuteChanged

  let setError error propName =
    match errors.TryGetValue propName with
    | true, err when err = error -> ()
    | _ ->
        log "[VM] Setting error for binding %s to \"%s\"" propName error
        errors.[propName] <- error
        errorsChanged.Trigger([| box this; box <| DataErrorsChangedEventArgs propName |])

  let removeError propName =
    if errors.Remove propName then
      log "[VM] Removing error for binding %s" propName
      errorsChanged.Trigger([| box this; box <| DataErrorsChangedEventArgs propName |])

  let initializeBinding bindingSpec =
    match bindingSpec with
    | OneWaySpec get -> OneWay get
    | OneWayLazySpec (get, map, equals) ->
        OneWayLazy (ref <| lazy (initialModel |> get |> map), get, map, equals)
    | OneWaySeqLazySpec (get, map, equals, getId, itemEquals) ->
        let vals = ObservableCollection(initialModel |> get |> map)
        OneWaySeq (vals, get, map, equals, getId, itemEquals)
    | TwoWaySpec (get, set) -> TwoWay (get, set)
    | TwoWayValidateSpec (get, set, validate) -> TwoWayValidate (get, set, validate)
    | TwoWayIfValidSpec (get, set) -> TwoWayIfValid (get, set)
    | CmdSpec (exec, canExec) ->
        let execute _ = exec currentModel |> dispatch
        let canExecute _ = canExec currentModel
        ParamCmd <| Command(execute, canExecute, false)
    | CmdIfValidSpec exec ->
        let execute _ = exec currentModel |> Result.iter dispatch
        let canExecute _ = exec currentModel |> Result.isOk
        CmdIfValid (Command(execute, canExecute, false), exec)
    | ParamCmdSpec (exec, canExec, autoRequery) ->
        let execute param = dispatch <| exec param currentModel
        let canExecute param = canExec param currentModel
        ParamCmd <| Command(execute, canExecute, autoRequery)
    | SubModelSpec (getModel, getBindings, toMsg) ->
        match getModel initialModel with
        | None -> SubModel (ref None, getModel, getBindings, toMsg)
        | Some m ->
            let vm = ViewModel(m, toMsg >> dispatch, getBindings (), config, uiDispatch)
            SubModel (ref <| Some vm, getModel, getBindings, toMsg)
    | SubModelSeqSpec (getModels, getId, getBindings, toMsg) ->
        let vms =
          getModels initialModel
          |> Seq.map (fun m ->
               ViewModel(m, (fun msg -> toMsg (getId m, msg) |> dispatch), getBindings (), config, uiDispatch)
          )
          |> ObservableCollection
        SubModelSeq (vms, getModels, getId, getBindings, toMsg)

  let setInitialError name = function
    | TwoWayValidate (_, _, validate) ->
        match validate initialModel with
        | Ok _ -> ()
        | Error error -> setError error name
    | TwoWayIfValid (get, set) ->
        let initialValue = get initialModel
        match set initialValue initialModel with
        | Ok _ -> ()
        | Error error -> setError error name
    | _ -> ()

  let bindings =
    let dict = Dictionary<string, Binding<'model, 'msg>>()
    for spec in bindingSpecs do
      log "[VM] Initializing binding %s" spec.Name
      let binding = initializeBinding spec.Data
      dict.Add(spec.Name, binding)
      setInitialError spec.Name binding
    dict

  /// Updates the binding value (for relevant bindings) and returns a value
  /// indicating whether to trigger PropertyChanged for this binding
  let updateValue newModel binding =
    match binding with
    | OneWay get
    | TwoWay (get, _)
    | TwoWayValidate (get, _, _)
    | TwoWayIfValid (get, _) ->
        get currentModel <> get newModel
    | OneWayLazy (currentVal, get, map, equals) ->
        if equals (get newModel) (get currentModel) then false
        else
          currentVal := lazy (newModel |> get |> map)
          true
    | OneWaySeq (vals, get', map, equals', getId, itemEquals) ->
        if not <| equals' (get' newModel) (get' currentModel) then
          let newVals = newModel |> get' |> map
          uiDispatch (fun () ->
            // Prune and update existing values
            let newLookup = Dictionary<_,_>()
            for v in newVals do newLookup.Add(getId v, v)
            for existingVal in vals |> Seq.toList do
              match newLookup.TryGetValue (getId existingVal) with
              | false, _ -> vals.Remove(existingVal) |> ignore
              | true, newVal when not (itemEquals newVal existingVal) ->
                  vals.Remove existingVal |> ignore
                  vals.Add newVal  // Will be sorted later
              | _ -> ()
            // Add new values that don't currently exist
            let valuesToAdd =
              newVals
              |> Seq.filter (fun m ->
                   vals |> Seq.exists (fun existingVal -> getId m = getId existingVal) |> not
              )
            for m in valuesToAdd do vals.Add m
            // Reorder according to new model list
            for newIdx, newVal in newVals |> Seq.indexed do
              let oldIdx =
                vals
                |> Seq.indexed
                |> Seq.find (fun (_, existingVal) -> getId existingVal = getId newVal)
                |> fst
              if oldIdx <> newIdx then vals.Move(oldIdx, newIdx)
          )
        false
    | Cmd _
    | CmdIfValid _
    | ParamCmd _ ->
        false
    | SubModel ((vm: ViewModel<obj, obj> option ref), (getModel: 'model -> obj option), getBindings, toMsg) ->
        match !vm, getModel newModel with
        | None, None -> false
        | Some _, None ->
            vm := None
            true
        | None, Some m ->
            vm := Some <| ViewModel(m, toMsg >> dispatch, getBindings (), config, uiDispatch)
            true
        | Some vm, Some m ->
            vm.UpdateModel(m)
            false
    | SubModelSeq (vms, getModels, getId, getBindings, toMsg) ->
        let newSubModels = getModels newModel
        uiDispatch (fun () ->
          // Prune and update existing models
          let newLookup = Dictionary<_,_>()
          for m in newSubModels do newLookup.Add(getId m, m)
          for vm in vms |> Seq.toList do
            match newLookup.TryGetValue (getId vm.CurrentModel) with
            | false, _ -> vms.Remove(vm) |> ignore
            | true, newSubModel -> vm.UpdateModel newSubModel
          // Add new models that don't currently exist
          let modelsToAdd =
            newSubModels
            |> Seq.filter (fun m ->
                 vms |> Seq.exists (fun vm -> getId m = getId vm.CurrentModel) |> not
            )
          for m in modelsToAdd do
            vms.Add <| ViewModel(m, (fun msg -> toMsg (getId m, msg) |> dispatch), getBindings (), config, uiDispatch)
          // Reorder according to new model list
          for newIdx, newSubModel in newSubModels |> Seq.indexed do
            let oldIdx =
              vms
              |> Seq.indexed
              |> Seq.find (fun (_, vm) -> getId newSubModel = getId vm.CurrentModel)
              |> fst
            if oldIdx <> newIdx then vms.Move(oldIdx, newIdx)
        )
        false

  /// Returns the command associated with a command binding if the command's
  /// CanExecuteChanged should be triggered.
  let getCmdIfCanExecChanged newModel binding =
    match binding with
    | OneWay _
    | OneWayLazy _
    | OneWaySeq _
    | TwoWay _
    | TwoWayValidate _
    | TwoWayIfValid _
    | SubModel _
    | SubModelSeq _ ->
        None
    | Cmd (cmd, canExec) ->
        if canExec newModel = canExec currentModel then None else Some cmd
    | CmdIfValid (cmd, exec) ->
        match exec currentModel, exec newModel with
        | Ok _, Error _ | Error _, Ok _ -> Some cmd
        | _ -> None
    | ParamCmd cmd -> Some cmd

  /// Updates the validation status for a binding.
  let updateValidationStatus name binding =
    match binding with
    | TwoWayValidate (_, _, validate) ->
        match validate currentModel with
        | Ok _ -> removeError name
        | Error err -> setError err name
    | _ -> ()

  member __.CurrentModel : 'model = currentModel

  member __.UpdateModel (newModel: 'model) : unit =
    log "[VM] UpdateModel %s" <| newModel.GetType().FullName
    let propsToNotify =
      bindings
      |> Seq.toList
      |> List.filter (Kvp.value >> updateValue newModel)
      |> List.map Kvp.key
    let cmdsToNotify =
      bindings
      |> Seq.toList
      |> List.choose (Kvp.value >> getCmdIfCanExecChanged newModel)
    currentModel <- newModel
    propsToNotify |> List.iter notifyPropertyChanged
    cmdsToNotify |> List.iter raiseCanExecuteChanged
    for Kvp (name, binding) in bindings do
      updateValidationStatus name binding

  override __.TryGetMember (binder, result) =
    log "[VM] TryGetMember %s" binder.Name
    match bindings.TryGetValue binder.Name with
    | false, _ ->
        log "[VM] TryGetMember FAILED: Property %s doesn't exist" binder.Name
        false
    | true, binding ->
        result <-
          match binding with
          | OneWay get
          | TwoWay (get, _)
          | TwoWayValidate (get, _, _)
          | TwoWayIfValid (get, _) ->
              get currentModel
          | OneWayLazy (value, _, _, _) ->
              (!value).Value
          | OneWaySeq (vals, _, _, _, _, _) ->
              box vals
          | Cmd (cmd, _)
          | CmdIfValid (cmd, _)
          | ParamCmd cmd ->
              box cmd
          | SubModel (vm, _, _, _) -> !vm |> Option.toObj |> box
          | SubModelSeq (vms, _, _, _, _) -> box vms
        true

  override __.TrySetMember (binder, value) =
    log "[VM] TrySetMember %s" binder.Name
    match bindings.TryGetValue binder.Name with
    | false, _ -> log "[VM] TrySetMember FAILED: Property %s doesn't exist" binder.Name
    | true, binding ->
        match binding with
        | TwoWay (_, set)
        | TwoWayValidate (_, set, _) ->
            dispatch <| set value currentModel
        | TwoWayIfValid (_, set) ->
            match set value currentModel with
            | Ok msg ->
                removeError binder.Name
                dispatch msg
            | Error err -> setError err binder.Name
        | OneWay _
        | OneWayLazy _
        | OneWaySeq _
        | Cmd _
        | CmdIfValid _
        | ParamCmd _
        | SubModel _
        | SubModelSeq _ ->
            log "[VM] TrySetMember FAILED: Binding %s is read-only" binder.Name
    // This function should always return false, otherwise the UI may execute a
    // subsequent get which may may return the old value
    false

  interface INotifyPropertyChanged with
    [<CLIEvent>]
    member __.PropertyChanged = propertyChanged.Publish

  interface INotifyDataErrorInfo with
    [<CLIEvent>]
    member __.ErrorsChanged = errorsChanged.Publish
    member __.HasErrors =
      errors.Count > 0
    member __.GetErrors propName =
      log "[VM] GetErrors %s" (propName |> Option.ofObj |> Option.defaultValue "<null>")
      match errors.TryGetValue propName with
      | true, err -> upcast [err]
      | false, _ -> upcast []
