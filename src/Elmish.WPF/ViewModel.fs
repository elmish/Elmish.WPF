namespace Elmish.WPF

open System
open System.Dynamic
open System.Collections.Generic
open System.Collections.ObjectModel
open System.ComponentModel
open System.Windows

open Elmish.WPF.Logging

/// Represents all necessary data used in an active binding.
type internal VmBinding<'model, 'msg> =
  | OneWay of
      get: ('model -> obj)
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
  | TwoWay of
      get: ('model -> obj)
      * set: (obj -> 'model -> 'msg)
  | TwoWayValidate of
      get: ('model -> obj)
      * set: (obj -> 'model -> 'msg)
      * validate: ('model -> string voption)
  | Cmd of
      cmd: Command
      * canExec: ('model -> bool)
  | CmdParam of cmd: Command
  | SubModel of
      vm: ViewModel<obj, obj> voption ref
      * getModel: ('model -> obj voption)
      * getBindings: (unit -> Binding<obj, obj> list)
      * toMsg: (obj -> 'msg)
      * sticky: bool
  | SubModelWin of
      vmWinState: WindowState<ViewModel<obj, obj>> ref
      * getState: ('model -> WindowState<obj>)
      * getBindings: (unit -> Binding<obj, obj> list)
      * toMsg: (obj -> 'msg)
      * getWindow: (unit -> Window)
      * isModal: bool
      * onCloseRequested: 'msg voption
      * preventClose: bool ref
      * windowRef: WeakReference<Window>
  | SubModelSeq of
      vms: ObservableCollection<ViewModel<obj, obj>>
      * getModels: ('model -> obj seq)
      * getId: (obj -> obj)
      * getBindings: (unit -> Binding<obj, obj> list)
      * toMsg: (obj * obj -> 'msg)
  | SubModelSelectedItem of
      selected: ViewModel<obj, obj> voption voption ref
      * get: ('model -> obj voption)
      * set: (obj voption -> 'model -> 'msg)
      * subModelSeqBindingName: string


and [<AllowNullLiteral>] internal ViewModel<'model, 'msg>
      ( initialModel: 'model,
        dispatch: 'msg -> unit,
        bindings: Binding<'model, 'msg> list,
        config: ElmConfig,
        propNameChain: string)
      as this =
  inherit DynamicObject()

  let logger =
    seq {
      if config.LogConsole then yield (fun (s: string) -> Console.WriteLine s)
      if config.LogTrace then yield Diagnostics.Trace.WriteLine
    } |> compositeLogger

  let logTrace = logger |> logTraceWith
  let logError = logger |> logErrorWith

  let mutable currentModel = initialModel

  let propertyChanged = Event<PropertyChangedEventHandler, PropertyChangedEventArgs>()
  let errorsChanged = DelegateEvent<EventHandler<DataErrorsChangedEventArgs>>()

  /// Error messages keyed by property name.
  let errors = Dictionary<string, string>()

  let getPropChainFor bindingName =
    sprintf "%s.%s" propNameChain bindingName

  let getPropChainForItem collectionBindingName itemId =
    sprintf "%s.%s.%s" propNameChain collectionBindingName itemId

  let notifyPropertyChanged propName =
    logTrace <| PropertyChanged {
      PropertyNameChain = propNameChain
      PropertyName = propName }
    propertyChanged.Trigger(this, PropertyChangedEventArgs propName)

  let raiseCanExecuteChanged (cmd: Command) =
    cmd.RaiseCanExecuteChanged ()

  let setError error propName =
    match errors.TryGetValue propName with
    | true, err when err = error -> ()
    | _ ->
        logTrace <| ValidationErrorsChanged {
          PropertyNameChain = propNameChain
          PropertyName = propName }
        errors.[propName] <- error
        errorsChanged.Trigger([| box this; box <| DataErrorsChangedEventArgs propName |])

  let removeError propName =
    if errors.Remove propName then
      logTrace <| ValidationErrorsChanged { // TODO: use new ValidationErrorsRemovedData
        PropertyNameChain = propNameChain
        PropertyName = propName }
      errorsChanged.Trigger([| box this; box <| DataErrorsChangedEventArgs propName |])

  let measure name callName f =
    if not config.Measure then f
    else
      fun x ->
        let sw = System.Diagnostics.Stopwatch.StartNew ()
        let r = f x
        sw.Stop ()
        if sw.ElapsedMilliseconds >= int64 config.MeasureLimitMs then
          logTrace <| Timing {
             PropertyNameChain = propNameChain
             PropertyName = name
             BindingDataFunctionName = callName
             ElapsedMilliseconds = sw.ElapsedMilliseconds
          }
        r

  let measure2 name callName f =
    if not config.Measure then f
    else
      fun x y ->
        let sw = System.Diagnostics.Stopwatch.StartNew ()
        let r = f x y
        sw.Stop ()
        if sw.ElapsedMilliseconds >= int64 config.MeasureLimitMs then
          logTrace <| Timing {
             PropertyNameChain = propNameChain
             PropertyName = name
             BindingDataFunctionName = callName
             ElapsedMilliseconds = sw.ElapsedMilliseconds
          }
        r

  let showNewWindow
      (winRef: WeakReference<Window>)
      (getWindow: unit -> Window)
      dataContext
      isDialog
      (onCloseRequested: 'msg voption)
      (preventClose: bool ref)
      initialVisibility
      dispatch =
    Application.Current.Dispatcher.Invoke(fun () ->
      let guiCtx = System.Threading.SynchronizationContext.Current
      async {
        let win = getWindow ()
        winRef.SetTarget win
        win.DataContext <- dataContext
        win.Closing.Add(fun ev ->
          ev.Cancel <- !preventClose
          async {
            do! Async.SwitchToThreadPool()
            onCloseRequested |> ValueOption.iter dispatch
          } |> Async.StartImmediate
        )
        do! Async.SwitchToContext guiCtx
        if isDialog then win.ShowDialog () |> ignore
        else
          win.Visibility <- initialVisibility
      } |> Async.StartImmediate
    )


  let initializeBinding name bindingData =
    match bindingData with
    | OneWayData d ->
        let get = measure name "get" d.Get
        OneWay get
    | OneWayLazyData d ->
        let get = measure name "get" d.Get
        let map = measure name "map" d.Map
        let equals = measure2 name "equals" d.Equals
        OneWayLazy (ref <| lazy (initialModel |> get |> map), get, map, equals)
    | OneWaySeqLazyData d ->
        let get = measure name "get" d.Get
        let map = measure name "map" d.Map
        let equals = measure2 name "equals" d.Equals
        let getId = measure name "getId" d.GetId
        let itemEquals = measure2 name "itemEquals" d.ItemEquals
        let vals = ObservableCollection(initialModel |> get |> map)
        OneWaySeq (vals, get, map, equals, getId, itemEquals)
    | TwoWayData d ->
        let get = measure name "get" d.Get
        let set = measure2 name "set" d.Set
        TwoWay (get, set)
    | TwoWayValidateData d ->
        let get = measure name "get" d.Get
        let set = measure2 name "set" d.Set
        let validate = measure name "validate" d.Validate
        TwoWayValidate (get, set, validate)
    | CmdData d ->
        let exec = measure name "exec" d.Exec
        let canExec = measure name "canExec" d.CanExec
        let execute _ = exec currentModel |> ValueOption.iter dispatch
        let canExecute _ = canExec currentModel
        Cmd (Command(execute, canExecute, false), canExec)
    | CmdParamData d ->
        let exec = measure2 name "exec" d.Exec
        let canExec = measure2 name "canExec" d.CanExec
        let execute param = exec param currentModel |> ValueOption.iter dispatch
        let canExecute param = canExec param currentModel
        CmdParam <| Command(execute, canExecute, d.AutoRequery)
    | SubModelData d ->
        let getModel = measure name "getSubModel" d.GetModel
        let getBindings = measure name "bindings" d.GetBindings
        let toMsg = measure name "toMsg" d.ToMsg
        match getModel initialModel with
        | ValueNone -> SubModel (ref ValueNone, getModel, getBindings, toMsg, d.Sticky)
        | ValueSome m ->
            let chain = getPropChainFor name
            let vm = ViewModel(m, toMsg >> dispatch, getBindings (), config, chain)
            SubModel (ref <| ValueSome vm, getModel, getBindings, toMsg, d.Sticky)
    | SubModelWinData d ->
        let getState = measure name "getState" d.GetState
        let getBindings = measure name "bindings" d.GetBindings
        let toMsg = measure name "toMsg" d.ToMsg
        match getState initialModel with
        | WindowState.Closed ->
            SubModelWin (
              ref WindowState.Closed,
              getState,
              getBindings,
              toMsg,
              d.GetWindow,
              d.IsModal,
              d.OnCloseRequested,
              ref true,
              WeakReference<_>(null))
        | WindowState.Hidden m ->
            let chain = getPropChainFor name
            let vm = ViewModel(m, toMsg >> dispatch, getBindings (), config, chain)
            let winRef = WeakReference<_>(null)
            let preventClose = ref true
            logTrace <| CreatingHiddenWindow { PropertyNameChain = chain }
            showNewWindow
              winRef d.GetWindow vm d.IsModal d.OnCloseRequested
              preventClose Visibility.Hidden dispatch
            SubModelWin (
              ref <| WindowState.Hidden vm,
              getState,
              getBindings,
              toMsg,
              d.GetWindow,
              d.IsModal,
              d.OnCloseRequested,
              preventClose,
              winRef
            )
        | WindowState.Visible m ->
            let chain = getPropChainFor name
            let vm = ViewModel(m, toMsg >> dispatch, getBindings (), config, chain)
            let winRef = WeakReference<_>(null)
            let preventClose = ref true
            logTrace <| CreatingVisibleWindow { PropertyNameChain = chain }
            showNewWindow
              winRef d.GetWindow vm d.IsModal d.OnCloseRequested
              preventClose Visibility.Visible dispatch
            SubModelWin (
              ref <| WindowState.Visible vm,
              getState,
              getBindings,
              toMsg,
              d.GetWindow,
              d.IsModal,
              d.OnCloseRequested,
              preventClose,
              winRef
            )
    | SubModelSeqData d ->
        let getModels = measure name "getSubModels" d.GetModels
        let getId = measure name "getId" d.GetId
        let getBindings = measure name "bindings" d.GetBindings
        let toMsg = measure name "toMsg" d.ToMsg
        let vms =
          getModels initialModel
          |> Seq.map (fun m ->
               let chain = getPropChainForItem name (getId m |> string)
               ViewModel(m, (fun msg -> toMsg (getId m, msg) |> dispatch), getBindings (), config, chain)
          )
          |> ObservableCollection
        SubModelSeq (vms, getModels, getId, getBindings, toMsg)
    | SubModelSelectedItemData d ->
        let get = measure name "get" d.Get
        let set = measure2 name "set" d.Set
        SubModelSelectedItem (ref ValueNone, get, set, d.SubModelSeqBindingName)

  let setInitialError name = function
    | TwoWayValidate (_, _, validate) ->
        match validate initialModel with
        | ValueNone -> ()
        | ValueSome error -> setError error name
    | _ -> ()

  let bindings =
    logTrace <| InitializingBindings { PropertyNameChain = propNameChain }
    let dict = Dictionary<string, VmBinding<'model, 'msg>>()
    for b in bindings do
      if dict.ContainsKey b.Name then failwithf "Binding name '%s' is duplicated" b.Name
      let binding = initializeBinding b.Name b.Data
      dict.Add(b.Name, binding)
      setInitialError b.Name binding
    dict

  let getSelectedSubModel model vms getSelectedId getSubModelId =
      vms
      |> Seq.tryFind (fun (vm: ViewModel<obj, obj>) ->
          getSelectedId model = ValueSome (getSubModelId vm.CurrentModel))
      |> ValueOption.ofOption

  /// Updates the binding value (for relevant bindings) and returns a value
  /// indicating whether to trigger PropertyChanged for this binding
  let updateValue bindingName newModel binding =
    match binding with
    | OneWay get
    | TwoWay (get, _)
    | TwoWayValidate (get, _, _) ->
        get currentModel <> get newModel
    | OneWayLazy (currentVal, get, map, equals) ->
        if equals (get newModel) (get currentModel) then false
        else
          currentVal := lazy (newModel |> get |> map)
          true
    | OneWaySeq (vals, get', map, equals', getId, itemEquals) ->
        if not <| equals' (get' newModel) (get' currentModel) then
          let newVals = newModel |> get' |> map
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
        false
    | Cmd _
    | CmdParam _ ->
        false
    | SubModel ((vm: ViewModel<obj, obj> voption ref), (getModel: 'model -> obj voption), getBindings, toMsg, sticky) ->
      match !vm, getModel newModel with
      | ValueNone, ValueNone -> false
      | ValueSome _, ValueNone ->
          if sticky then false
          else
            vm := ValueNone
            true
      | ValueNone, ValueSome m ->
          vm := ValueSome <| ViewModel(
            m, toMsg >> dispatch, getBindings (), config, getPropChainFor bindingName)
          true
      | ValueSome vm, ValueSome m ->
          vm.UpdateModel m
          false
    | SubModelWin
        ( vmWinState, getState, getBindings, toMsg,
          getWindow, isModal, onCloseRequested, preventClose, winRef) ->

        let winPropChain = getPropChainFor bindingName

        let close () =
          preventClose := false
          match winRef.TryGetTarget () with
          | false, _ ->
              logError <| WindowToCloseMissing { PropertyNameChain = winPropChain }
          | true, w ->
              logTrace <| ClosingWindow { PropertyNameChain = winPropChain }
              winRef.SetTarget null
              w.Close ()
          winRef.SetTarget null

        let hide () =
          match winRef.TryGetTarget () with
          | false, _ ->
              logError <| WindowToHideMissing { PropertyNameChain = winPropChain }
          | true, w ->
              logTrace <| HidingWindow { PropertyNameChain = winPropChain }
              w.Visibility <- Visibility.Hidden

        let showHidden () =
          match winRef.TryGetTarget () with
          | false, _ ->
              logError <| WindowToShowMissing { PropertyNameChain = winPropChain }
          | true, w ->
              logTrace <| ShowingHiddenWindow { PropertyNameChain = winPropChain }
              w.Visibility <- Visibility.Visible

        let showNew vm initialVisibility =
          preventClose := true
          showNewWindow
            winRef getWindow vm isModal onCloseRequested
            preventClose initialVisibility dispatch

        let newVm model =
          ViewModel(model, toMsg >> dispatch, getBindings (), config, getPropChainFor bindingName)

        match !vmWinState, getState newModel with
        | WindowState.Closed, WindowState.Closed ->
            false
        | WindowState.Hidden _, WindowState.Closed
        | WindowState.Visible _, WindowState.Closed ->
            close ()
            vmWinState := WindowState.Closed
            true
        | WindowState.Closed, WindowState.Hidden m ->
            let vm = newVm m
            logTrace <| CreatingHiddenWindow { PropertyNameChain = winPropChain }
            showNew vm Visibility.Hidden
            vmWinState := WindowState.Hidden vm
            true
        | WindowState.Hidden vm, WindowState.Hidden m ->
            vm.UpdateModel m
            false
        | WindowState.Visible vm, WindowState.Hidden m ->
            hide ()
            vm.UpdateModel m
            vmWinState := WindowState.Hidden vm
            false
        | WindowState.Closed, WindowState.Visible m ->
            let vm = newVm m
            logTrace <| CreatingVisibleWindow { PropertyNameChain = winPropChain }
            showNew vm Visibility.Visible
            vmWinState := WindowState.Visible vm
            true
        | WindowState.Hidden vm, WindowState.Visible m ->
            vm.UpdateModel m
            showHidden ()
            vmWinState := WindowState.Visible vm
            false
        | WindowState.Visible vm, WindowState.Visible m ->
            vm.UpdateModel m
            false
    | SubModelSeq (vms, getModels, getId, getBindings, toMsg) ->
        let newSubModels = getModels newModel
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
          let chain = getPropChainForItem bindingName (getId m |> string)
          vms.Add <| ViewModel(
            m, (fun msg -> toMsg (getId m, msg) |> dispatch), getBindings (), config, chain)
        // Reorder according to new model list
        for newIdx, newSubModel in newSubModels |> Seq.indexed do
          let oldIdx =
            vms
            |> Seq.indexed
            |> Seq.find (fun (_, vm) -> getId newSubModel = getId vm.CurrentModel)
            |> fst
          if oldIdx <> newIdx then vms.Move(oldIdx, newIdx)
        false
    | SubModelSelectedItem (vm, getSelectedId, _, name) ->
        match bindings.TryGetValue name with
        | true, SubModelSeq (vms, _, getSubModelId, _, _) ->
            let v = getSelectedSubModel newModel vms getSelectedId getSubModelId
            logTrace <| NewSubModelSelected {
              PropertyNameChain = propNameChain
              NewSelection = (v |> ValueOption.map (fun v -> getSubModelId v.CurrentModel))
            }
            vm := ValueSome v
        | _ -> failwithf "subModelSelectedItem binding referenced binding '%s', but no compatible binding was found with that name" name
        true

  /// Returns the command associated with a command binding if the command's
  /// CanExecuteChanged should be triggered.
  let getCmdIfCanExecChanged newModel binding =
    match binding with
    | OneWay _
    | OneWayLazy _
    | OneWaySeq _
    | TwoWay _
    | TwoWayValidate _
    | SubModel _
    | SubModelWin _
    | SubModelSeq _
    | SubModelSelectedItem _ ->
        None
    | Cmd (cmd, canExec) ->
        if canExec newModel = canExec currentModel then None else Some cmd
    | CmdParam cmd -> Some cmd

  /// Updates the validation status for a binding.
  let updateValidationStatus name binding =
    match binding with
    | TwoWayValidate (_, _, validate) ->
        match validate currentModel with
        | ValueNone -> removeError name
        | ValueSome err -> setError err name
    | _ -> ()

  member __.CurrentModel : 'model = currentModel

  member __.UpdateModel (newModel: 'model) : unit =
    let propsToNotify =
      bindings
      |> Seq.toList
      |> List.filter (fun (Kvp (name, binding)) -> updateValue name newModel binding)
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
    logTrace <| TryGetMemberCalled {
      PropertyNameChain = propNameChain
      PropertyName = binder.Name
    }
    match bindings.TryGetValue binder.Name with
    | false, _ ->
        logError <| TryGetMemberMissingBinding {
          PropertyNameChain = propNameChain
          PropertyName = binder.Name
        }
        false
    | true, binding ->
        result <-
          match binding with
          | OneWay get
          | TwoWay (get, _)
          | TwoWayValidate (get, _, _) ->
              get currentModel
          | OneWayLazy (value, _, _, _) ->
              (!value).Value
          | OneWaySeq (vals, _, _, _, _, _) ->
              box vals
          | Cmd (cmd, _)
          | CmdParam cmd ->
              box cmd
          | SubModel (vm, _, _, _, _) -> !vm |> ValueOption.toObj |> box
          | SubModelWin (vm, _, _, _, _, _, _, _, _) ->
              match !vm with
              | WindowState.Closed -> null
              | WindowState.Hidden vm | WindowState.Visible vm -> box vm
          | SubModelSeq (vms, _, _, _, _) -> box vms
          | SubModelSelectedItem (vm, getSelectedId, _, name) ->
              match !vm with
              | ValueSome x -> x |> ValueOption.toObj |> box
              | ValueNone ->
                  // No computed value, must perform initial computation
                  match bindings.TryGetValue name with
                  | true, SubModelSeq (vms, _, getSubModelId, _, _) ->
                      let selected = getSelectedSubModel currentModel vms getSelectedId getSubModelId
                      vm := ValueSome selected
                      selected |> ValueOption.toObj |> box
                  | _ ->
                    failwithf "subModelSelectedItem binding '%s' referenced binding '%s', but no compatible binding was found with that name" binder.Name name
        true

  override __.TrySetMember (binder, value) =
    logTrace <| TrySetMemberCalled {
      PropertyNameChain = propNameChain
      PropertyName = binder.Name
    }
    match bindings.TryGetValue binder.Name with
    | false, _ ->
        logError <| TrySetMemberMissingBinding {
          PropertyNameChain = propNameChain
          PropertyName = binder.Name
        }
        false
    | true, binding ->
        match binding with
        | TwoWay (_, set)
        | TwoWayValidate (_, set, _) ->
            dispatch <| set value currentModel
            true
        | SubModelSelectedItem (_, _, set, name) ->
            match bindings.TryGetValue name with
            | true, SubModelSeq (_, _, getSubModelId, _, _) ->
                let value =
                  (value :?> ViewModel<obj, obj>)
                  |> ValueOption.ofObj
                  |> ValueOption.map (fun vm -> getSubModelId vm.CurrentModel)
                set value currentModel |> dispatch
                true
            | _ ->
                failwithf "subModelSelectedItem binding '%s' referenced binding '%s', but no compatible binding was found with that name" binder.Name name
        | OneWay _
        | OneWayLazy _
        | OneWaySeq _
        | Cmd _
        | CmdParam _
        | SubModel _
        | SubModelWin _
        | SubModelSeq _ ->
            logError <| TrySetMemberReadOnlyBinding {
              PropertyNameChain = propNameChain
              PropertyName = binder.Name
            }
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
      logTrace <| GettingErrors {
        PropertyNameChain = propNameChain
        PropertyName = propName |> Option.ofObj
      }
      match errors.TryGetValue propName with
      | true, err -> upcast [err]
      | false, _ -> upcast []
