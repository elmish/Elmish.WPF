namespace Elmish.WPF

open System
open System.Dynamic
open System.Collections.Generic
open System.Collections.ObjectModel
open System.ComponentModel
open System.Windows
open Microsoft.Extensions.Logging

open Elmish


type internal UpdateData =
  | ErrorsChanged
  | PropertyChanged
  | CanExecuteChanged of Command


type internal OneWayBinding<'model, 'a when 'a : equality> = {
  OneWayData: OneWayData<'model, 'a>
}

type internal OneWayLazyBinding<'model, 'a, 'b> = {
  OneWayLazyData: OneWayLazyData<'model, 'a, 'b>
}

type internal OneWaySeqBinding<'model, 'a, 'b, 'id when 'id : equality> = {
  OneWaySeqData: OneWaySeqLazyData<'model, 'a, 'b, 'id> // TODO: consider renaming so that both contain "Lazy" or neither do
  Values: ObservableCollection<'b>
}

type internal TwoWayBinding<'model, 'msg, 'a when 'a : equality> = {
  TwoWayData: TwoWayData<'model, 'msg, 'a>
}

type internal SubModelBinding<'model, 'msg, 'bindingModel, 'bindingMsg> = {
  SubModelData: SubModelData<'model, 'msg, 'bindingModel, 'bindingMsg>
  Vm: ViewModel<'bindingModel, 'bindingMsg> voption ref
}

and internal SubModelWinBinding<'model, 'msg, 'bindingModel, 'bindingMsg> = {
  SubModelWinData: SubModelWinData<'model, 'msg, 'bindingModel, 'bindingMsg>
  WinRef: WeakReference<Window>
  PreventClose: bool ref
  VmWinState: WindowState<ViewModel<'bindingModel, 'bindingMsg>> ref
}

and internal SubModelSeqBinding<'model, 'msg, 'bindingModel, 'bindingMsg, 'id when 'id : equality> = {
  SubModelSeqData: SubModelSeqData<'model, 'msg, 'bindingModel, 'bindingMsg, 'id>
  Vms: ObservableCollection<ViewModel<'bindingModel, 'bindingMsg>>
}

and internal SubModelSelectedItemBinding<'model, 'msg, 'bindingModel, 'bindingMsg, 'id when 'id : equality> = {
  SubModelSelectedItemData: SubModelSelectedItemData<'model, 'msg, 'id>
  SubModelSeqBinding: SubModelSeqBinding<'model, 'msg, 'bindingModel, 'bindingMsg, 'id>
}

and internal CachedBinding<'model, 'msg, 'value> = {
  Binding: VmBinding<'model, 'msg>
  Cache: 'value option ref
}

and internal ValidationBinding<'model, 'msg> = {
  Binding: VmBinding<'model, 'msg>
  Validate: 'model -> string list
  Errors: string list ref
}

and internal LazyBinding<'model, 'msg> = {
  Binding: VmBinding<'model, 'msg>
  Equals: 'model -> 'model -> bool
}


/// Represents all necessary data used in an active binding.
and internal VmBinding<'model, 'msg> =
  | OneWay of OneWayBinding<'model, obj>
  | OneWayLazy of OneWayLazyBinding<'model, obj, obj>
  | OneWaySeq of OneWaySeqBinding<'model, obj, obj, obj>
  | TwoWay of TwoWayBinding<'model, 'msg, obj>
  | Cmd of cmd: Command
  | SubModel of SubModelBinding<'model, 'msg, obj, obj>
  | SubModelWin of SubModelWinBinding<'model, 'msg, obj, obj>
  | SubModelSeq of SubModelSeqBinding<'model, 'msg, obj, obj, obj>
  | SubModelSelectedItem of SubModelSelectedItemBinding<'model, 'msg, obj, obj, obj>
  | Cached of CachedBinding<'model, 'msg, obj>
  | Validatation of ValidationBinding<'model, 'msg>
  | Lazy of LazyBinding<'model, 'msg>


and [<AllowNullLiteral>] internal ViewModel<'model, 'msg>
      ( initialModel: 'model,
        dispatch: 'msg -> unit,
        bindings: Binding<'model, 'msg> list,
        performanceLogThresholdMs: int,
        nameChain: string,
        log: ILogger,
        logPerformance: ILogger)
      as this =
  inherit DynamicObject()

  let mutable currentModel = initialModel

  let propertyChanged = Event<PropertyChangedEventHandler, PropertyChangedEventArgs>()
  let errorsChanged = DelegateEvent<EventHandler<DataErrorsChangedEventArgs>>()


  let withCaching b = Cached { Binding = b; Cache = ref None }
  let addLazy equals b = { Binding = b; Equals = equals } |> Lazy


  let getNameChainFor name =
    sprintf "%s.%s" nameChain name
  let getNameChainForItem collectionBindingName itemId =
    sprintf "%s.%s.%s" nameChain collectionBindingName itemId

  let raisePropertyChanged name =
    log.LogTrace("[{BindingNameChain}] PropertyChanged {BindingName}", nameChain, name)
    propertyChanged.Trigger(this, PropertyChangedEventArgs name)
  let raiseCanExecuteChanged (cmd: Command) =
    cmd.RaiseCanExecuteChanged ()
  let raiseErrorsChanged name =
    log.LogTrace("[{BindingNameChain}] ErrorsChanged {BindingName}", nameChain, name)
    errorsChanged.Trigger([| box this; box <| DataErrorsChangedEventArgs name |])

  let measure name callName f =
    if not <| logPerformance.IsEnabled(LogLevel.Trace) then f
    else
      fun x ->
        let sw = System.Diagnostics.Stopwatch.StartNew ()
        let r = f x
        sw.Stop ()
        if sw.ElapsedMilliseconds >= int64 performanceLogThresholdMs then
          logPerformance.LogTrace("[{BindingNameChain}] {CallName} ({Elapsed}ms): {MeasureName}", nameChain, callName, sw.ElapsedMilliseconds, name)
        r

  let measure2 name callName f =
    if not <| logPerformance.IsEnabled(LogLevel.Trace) then f
    else fun x -> measure name callName (f x)

  let showNewWindow
      (winRef: WeakReference<Window>)
      (getWindow: 'model -> Dispatch<'msg> -> Window)
      isDialog
      onCloseRequested
      (preventClose: bool ref)
      dataContext
      initialVisibility =
    let win = getWindow currentModel dispatch
    winRef.SetTarget win
    (*
     * A different thread might own this Window, so must use its Dispatcher.
     * Invoking asynchronously since ShowDialog is a blocking call. Otherwise,
     * invoking ShowDialog synchronously blocks the Elmish dispatch loop.
     *)
    win.Dispatcher.InvokeAsync(fun () ->
      win.DataContext <- dataContext
      win.Closing.Add(fun ev ->
        ev.Cancel <- !preventClose
        currentModel |> onCloseRequested |> ValueOption.iter dispatch
      )
      if isDialog then
        win.ShowDialog () |> ignore
      else
        (*
         * Calling Show achieves the same end result as setting Visibility
         * property of the Window object to Visible. However, there is a
         * difference between the two from a timing perspective.
         *
         * Calling Show is a synchronous operation that returns only after
         * the Loaded event on the child window has been raised.
         *
         * Setting Visibility, however, is an asynchronous operation that
         * returns immediately
         * https://docs.microsoft.com/en-us/dotnet/api/system.windows.window.show
         *)
        win.Visibility <- initialVisibility
    ) |> ignore

  let initializeBinding name getInitializedBindingByName addValdiationBinding =
    let measure x = x |> measure name
    let measure2 x = x |> measure2 name
    let rec initializeBindingRec = function
      | OneWayData d ->
          { OneWayData = d |> BindingData.OneWay.measureFunctions measure }
          |> OneWay
          |> Some 
      | OneWayLazyData d ->
          { OneWayLazyData = d |> BindingData.OneWayLazy.measureFunctions measure measure measure2 }
          |> OneWayLazy
          |> withCaching
          |> Some
      | OneWaySeqLazyData d ->
          { OneWaySeqData = d |> BindingData.OneWaySeqLazy.measureFunctions measure measure measure2 measure measure2
            Values = ObservableCollection(initialModel |> d.Get |> d.Map) }
          |> OneWaySeq
          |> Some
      | TwoWayData d ->
          { TwoWayData = d |> BindingData.TwoWay.measureFunctions measure measure }
          |> TwoWay
          |> Some
      | CmdData d ->
          let d = d |> BindingData.Cmd.measureFunctions measure measure
          let execute _ = d.Exec currentModel |> ValueOption.iter dispatch
          let canExecute _ = d.CanExec currentModel
          Command(execute, canExecute, false)
          |> Cmd
          |> addLazy (fun a b -> d.CanExec a = d.CanExec b)
          |> Some
      | CmdParamData d ->
          let d = d |> BindingData.CmdParam.measureFunctions measure2 measure2
          let execute param = d.Exec param currentModel |> ValueOption.iter dispatch
          let canExecute param = d.CanExec param currentModel
          Command(execute, canExecute, d.AutoRequery)
          |> Cmd
          |> Some
      | SubModelData d ->
          let d = d |> BindingData.SubModel.measureFunctions measure measure measure2
          let toMsg = fun msg -> d.ToMsg currentModel msg
          d.GetModel initialModel
          |> ValueOption.map (fun m -> ViewModel(m, toMsg >> dispatch, d.GetBindings (), performanceLogThresholdMs, getNameChainFor name, log, logPerformance))
          |> (fun vm -> { SubModelData = d; Vm = ref vm })
          |> SubModel
          |> Some
      | SubModelWinData d ->
          let d = d |> BindingData.SubModelWin.measureFunctions measure measure measure2
          let toMsg = fun msg -> d.ToMsg currentModel msg
          match d.GetState initialModel with
          | WindowState.Closed ->
              { SubModelWinData = d
                WinRef = WeakReference<_>(null)
                PreventClose = ref true
                VmWinState = ref WindowState.Closed }
          | WindowState.Hidden m ->
              let chain = getNameChainFor name
              let vm = ViewModel(m, toMsg >> dispatch, d.GetBindings (), performanceLogThresholdMs, chain, log, logPerformance)
              let winRef = WeakReference<_>(null)
              let preventClose = ref true
              log.LogTrace("[{BindingNameChain}] Creating hidden window", chain)
              showNewWindow winRef d.GetWindow d.IsModal d.OnCloseRequested preventClose vm Visibility.Hidden
              { SubModelWinData = d
                WinRef = winRef
                PreventClose = preventClose
                VmWinState = ref <| WindowState.Hidden vm }
          | WindowState.Visible m ->
              let chain = getNameChainFor name
              let vm = ViewModel(m, toMsg >> dispatch, d.GetBindings (), performanceLogThresholdMs, chain, log, logPerformance)
              let winRef = WeakReference<_>(null)
              let preventClose = ref true
              log.LogTrace("[{BindingNameChain}] Creating visible window", chain)
              showNewWindow winRef d.GetWindow d.IsModal d.OnCloseRequested preventClose vm Visibility.Visible
              { SubModelWinData = d
                WinRef = winRef
                PreventClose = preventClose
                VmWinState = ref <| WindowState.Visible vm }
          |> SubModelWin
          |> Some
      | SubModelSeqData d ->
          let d = d |> BindingData.SubModelSeq.measureFunctions measure measure measure measure2
          let toMsg = fun msg -> d.ToMsg currentModel msg
          let vms =
            d.GetModels initialModel
            |> Seq.map (fun m ->
                 let chain = getNameChainForItem name (d.GetId m |> string)
                 ViewModel(m, (fun msg -> toMsg (d.GetId m, msg) |> dispatch), d.GetBindings (), performanceLogThresholdMs, chain, log, logPerformance)
            )
            |> ObservableCollection
          { SubModelSeqData = d
            Vms = vms }
          |> SubModelSeq
          |> Some
      | SubModelSelectedItemData d ->
          let d = d |> BindingData.SubModelSelectedItem.measureFunctions measure measure2
          match getInitializedBindingByName d.SubModelSeqBindingName with
          | Some (SubModelSeq b) ->
              { SubModelSelectedItemData = d
                SubModelSeqBinding = b }
              |> SubModelSelectedItem
              |> withCaching
              |> Some
          | Some _ ->
              log.LogError("SubModelSelectedItem binding referenced binding {SubModelSeqBindingName} but it is not a SubModelSeq binding", d.SubModelSeqBindingName)
              None
          | None ->
              log.LogError("SubModelSelectedItem binding referenced binding {SubModelSeqBindingName} but no binding was found with that name", d.SubModelSeqBindingName)
              None
      | ValidationData d ->
          let d = d |> BindingData.Validation.measureFunctions measure
          d.BindingData
          |> initializeBindingRec
          |> Option.map (fun b ->
            let binding =
              { Binding = b
                Validate = d.Validate
                Errors = currentModel |> d.Validate |> ref }
            addValdiationBinding name binding
            Validatation binding)
      | LazyData d ->
          let d = d |> BindingData.Lazy.measureFunctions measure
          d.BindingData
          |> initializeBindingRec
          |> Option.map (addLazy d.Equals)
    initializeBindingRec

  let (bindings, validationBindingsByName) =
    log.LogTrace("[{BindingNameChain}] Initializing bindings", nameChain)
    let bindingDict = Dictionary<string, VmBinding<'model, 'msg>>(bindings.Length)
    let validationDict = Dictionary<string, ValidationBinding<'model, 'msg>>()
    let bindingDictAsFunc = flip Dictionary.tryFind bindingDict
    let validationDictAsFunc k v = validationDict.[k] <- v
    let sortedBindings = bindings |> List.sortWith Binding.subModelSelectedItemLast
    for b in sortedBindings do
      if bindingDict.ContainsKey b.Name then
        log.LogError("Binding name {BindingName} is duplicated. Only the first occurrence will be used.", b.Name)
      else
        initializeBinding b.Name bindingDictAsFunc validationDictAsFunc b.Data
        |> Option.iter (fun binding ->
          bindingDict.Add(b.Name, binding))
    (bindingDict    :> IReadOnlyDictionary<_,_>,
     validationDict :> IReadOnlyDictionary<_,_>)

  /// Updates the binding and returns a list indicating what events to raise
  /// for this binding
  let updateBinding name newModel =
    let rec updateBindingRec = function
      | OneWay { OneWayData = d } ->
          d.DidPropertyChange(currentModel, newModel)
          |> Option.fromBool PropertyChanged
          |> Option.toList
      | TwoWay { TwoWayData = d } ->
          d.DidPropertyChange(currentModel, newModel)
          |> Option.fromBool PropertyChanged
          |> Option.toList
      | OneWayLazy { OneWayLazyData = d } ->
          d.DidProeprtyChange(currentModel, newModel)
          |> Option.fromBool PropertyChanged
          |> Option.toList
      | OneWaySeq b ->
          b.OneWaySeqData.Merge(b.Values, currentModel, newModel)
          []
      | Cmd cmd ->
          cmd |> CanExecuteChanged |> List.singleton
      | SubModel b ->
        let d = b.SubModelData
        match !b.Vm, d.GetModel newModel with
        | ValueNone, ValueNone -> []
        | ValueSome _, ValueNone ->
            if d.Sticky then []
            else
              b.Vm := ValueNone
              PropertyChanged |> List.singleton
        | ValueNone, ValueSome m ->
            let toMsg = fun msg -> d.ToMsg currentModel msg
            b.Vm := ValueSome <| ViewModel(m, toMsg >> dispatch, d.GetBindings (), performanceLogThresholdMs, getNameChainFor name, log, logPerformance)
            PropertyChanged |> List.singleton
        | ValueSome vm, ValueSome m ->
            vm.UpdateModel m
            []
      | SubModelWin b ->
          let d = b.SubModelWinData
          let winPropChain = getNameChainFor name
          let close () =
            b.PreventClose := false
            match b.WinRef.TryGetTarget () with
            | false, _ ->
                log.LogError("[{BindingNameChain}] Attempted to close window, but did not find window reference", winPropChain)
            | true, w ->
                log.LogTrace("[{BindingNameChain}] Closing window", winPropChain)
                b.WinRef.SetTarget null
                (*
                 * The Window might be in the process of closing,
                 * so instead of immediately exeucting Window.Close via Dispatcher.Invoke,
                 * queue a call to Window.Close via Dispatcher.InvokeAsync.
                 * https://github.com/elmish/Elmish.WPF/issues/330
                 *)
                w.Dispatcher.InvokeAsync(w.Close) |> ignore
            b.WinRef.SetTarget null

          let hide () =
            match b.WinRef.TryGetTarget () with
            | false, _ ->
                log.LogError("[{BindingNameChain}] Attempted to hide window, but did not find window reference", winPropChain)
            | true, w ->
                log.LogTrace("[{BindingNameChain}] Hiding window", winPropChain)
                w.Dispatcher.Invoke(fun () -> w.Visibility <- Visibility.Hidden)

          let showHidden () =
            match b.WinRef.TryGetTarget () with
            | false, _ ->
                log.LogError("[{BindingNameChain}] Attempted to show existing hidden window, but did not find window reference", winPropChain)
            | true, w ->
                log.LogTrace("[{BindingNameChain}] Showing existing hidden window", winPropChain)
                w.Dispatcher.Invoke(fun () -> w.Visibility <- Visibility.Visible)

          let showNew vm =
            b.PreventClose := true
            showNewWindow b.WinRef d.GetWindow d.IsModal d.OnCloseRequested b.PreventClose vm

          let newVm model =
            let toMsg = fun msg -> d.ToMsg currentModel msg
            ViewModel(model, toMsg >> dispatch, d.GetBindings (), performanceLogThresholdMs, getNameChainFor name, log, logPerformance)

          match !b.VmWinState, d.GetState newModel with
          | WindowState.Closed, WindowState.Closed ->
              []
          | WindowState.Hidden vm, WindowState.Hidden m
          | WindowState.Visible vm, WindowState.Visible m ->
              vm.UpdateModel m
              []
          | WindowState.Hidden _, WindowState.Closed
          | WindowState.Visible _, WindowState.Closed ->
              close ()
              b.VmWinState := WindowState.Closed
              PropertyChanged |> List.singleton
          | WindowState.Visible vm, WindowState.Hidden m ->
              hide ()
              vm.UpdateModel m
              b.VmWinState := WindowState.Hidden vm
              []
          | WindowState.Hidden vm, WindowState.Visible m ->
              vm.UpdateModel m
              showHidden ()
              b.VmWinState := WindowState.Visible vm
              []
          | WindowState.Closed, WindowState.Hidden m ->
              let vm = newVm m
              log.LogTrace("[{BindingNameChain}] Creating hidden window", winPropChain)
              showNew vm Visibility.Hidden
              b.VmWinState := WindowState.Hidden vm
              PropertyChanged |> List.singleton
          | WindowState.Closed, WindowState.Visible m ->
              let vm = newVm m
              log.LogTrace("[{BindingNameChain}] Creating visible window", winPropChain)
              showNew vm Visibility.Visible
              b.VmWinState := WindowState.Visible vm
              PropertyChanged |> List.singleton
      | SubModelSeq b ->
          let d = b.SubModelSeqData
          let getTargetId getId (vm: ViewModel<_, _>) = getId vm.CurrentModel
          let create m id = 
            let toMsg = fun msg -> d.ToMsg currentModel msg
            let chain = getNameChainForItem name (id |> string)
            ViewModel(m, (fun msg -> toMsg (id, msg) |> dispatch), d.GetBindings (), performanceLogThresholdMs, chain, log, logPerformance)
          let update (vm: ViewModel<_, _>) = vm.UpdateModel
          d.Merge(getTargetId, create, update, b.Vms, newModel)
          []
      | SubModelSelectedItem { SubModelSelectedItemData = d } ->
          d.DidPropertyChange(currentModel, newModel)
          |> Option.fromBool PropertyChanged
          |> Option.toList
      | Cached b ->
          let updates = updateBindingRec b.Binding
          updates
          |> List.filter ((=) PropertyChanged)
          |> List.iter (fun _ -> b.Cache := None)
          updates
      | Validatation b ->
          let updates = updateBindingRec b.Binding
          let newErrors = b.Validate newModel
          if !b.Errors <> newErrors then
            b.Errors := newErrors
            ErrorsChanged :: updates
          else
            updates
      | Lazy b ->
          if b.Equals currentModel newModel then
            []
          else
            updateBindingRec b.Binding
    updateBindingRec

  let tryGetMember model =
    let rec tryGetMemberRec = function
      | OneWay { OneWayData = d } -> d.TryGetMember model
      | TwoWay { TwoWayData = d } -> d.TryGetMember model
      | OneWayLazy { OneWayLazyData = d } -> d.TryGetMember model
      | OneWaySeq { Values = vals } -> box vals
      | Cmd cmd -> box cmd
      | SubModel { Vm = vm } -> !vm |> ValueOption.toObj |> box
      | SubModelWin { VmWinState = vm } ->
          !vm
          |> WindowState.toVOption
          |> ValueOption.map box
          |> ValueOption.toObj
      | SubModelSeq { Vms = vms } -> box vms
      | SubModelSelectedItem b ->
          let selected =
            b.SubModelSelectedItemData.TryGetMember
              ((fun (vm: ViewModel<_, _>) -> vm.CurrentModel),
               b.SubModelSeqBinding.SubModelSeqData,
               b.SubModelSeqBinding.Vms,
               model)
          log.LogTrace(
            "[{BindingNameChain}] Setting selected VM to {SubModelId}",
            nameChain,
            (selected |> Option.map (fun vm -> b.SubModelSeqBinding.SubModelSeqData.GetId vm.CurrentModel))
          )
          selected |> Option.toObj |> box
      | Cached b ->
          match !b.Cache with
          | Some v -> v
          | None ->
              let v = tryGetMemberRec b.Binding
              b.Cache := Some v
              v
      | Validatation b ->
          tryGetMemberRec b.Binding
      | Lazy b ->
          tryGetMemberRec b.Binding
    tryGetMemberRec

  let trySetMember model (value: obj) =
    let rec trySetMemberRec = function // TOOD: return 'msg option
      | TwoWay { TwoWayData = d } ->
          d.TrySetMember(value, model) |> dispatch
          true
      | SubModelSelectedItem b ->
          let bindingModel =
            (value :?> ViewModel<obj, obj>)
            |> ValueOption.ofObj
            |> ValueOption.map (fun vm -> vm.CurrentModel)
          b.SubModelSelectedItemData.TrySetMember(b.SubModelSeqBinding.SubModelSeqData, model, bindingModel) |> dispatch
          true
      | Cached b ->
          let successful = trySetMemberRec b.Binding
          if successful then
            b.Cache := None  // TODO #185: write test
          successful
      | Validatation b ->
          trySetMemberRec b.Binding
      | Lazy b ->
          trySetMemberRec b.Binding
      | OneWay _
      | OneWayLazy _
      | OneWaySeq _
      | Cmd _
      | SubModel _
      | SubModelWin _
      | SubModelSeq _ ->
          false
    trySetMemberRec

  member internal _.CurrentModel : 'model = currentModel

  member internal _.UpdateModel (newModel: 'model) : unit =
    let eventsToRaise =
      bindings
      |> Seq.collect (fun (Kvp (name, binding)) ->
        updateBinding name newModel binding
        |> Seq.map (fun ud -> name, ud))
      |> Seq.toList
    currentModel <- newModel
    eventsToRaise
    |> List.iter (fun (name, updateData) ->
      match updateData with
      | ErrorsChanged -> raiseErrorsChanged name
      | PropertyChanged -> raisePropertyChanged name
      | CanExecuteChanged cmd -> cmd |> raiseCanExecuteChanged)

  override _.TryGetMember (binder, result) =
    log.LogTrace("[{BindingNameChain}] TryGetMember {BindingName}", nameChain, binder.Name)
    match bindings.TryGetValue binder.Name with
    | false, _ ->
        log.LogError("[{BindingNameChain}] TryGetMember FAILED: Property {BindingName} doesn't exist", nameChain, binder.Name)
        false
    | true, binding ->
        result <- tryGetMember currentModel binding
        true

  override _.TrySetMember (binder, value) =
    log.LogTrace("[{BindingNameChain}] TrySetMember {BindingName}", nameChain, binder.Name)
    match bindings.TryGetValue binder.Name with
    | false, _ ->
        log.LogError("[{BindingNameChain}] TrySetMember FAILED: Property {BindingName} doesn't exist", nameChain, binder.Name)
        false
    | true, binding ->
        let success = trySetMember currentModel value binding
        if not success then
          log.LogError("[{BindingNameChain}] TrySetMember FAILED: Binding {BindingName} is read-only", nameChain, binder.Name)
        success


  interface INotifyPropertyChanged with
    [<CLIEvent>]
    member _.PropertyChanged = propertyChanged.Publish

  interface INotifyDataErrorInfo with
    [<CLIEvent>]
    member _.ErrorsChanged = errorsChanged.Publish
    member _.HasErrors =
      // WPF calls this too often, so don't log https://github.com/elmish/Elmish.WPF/issues/354
      validationBindingsByName
      |> Seq.map (fun (Kvp(_, b)) -> !b.Errors)
      |> Seq.filter (not << List.isEmpty)
      |> (not << Seq.isEmpty)
    member _.GetErrors name =
      log.LogTrace("[{BindingNameChain}] GetErrors {BindingName}", nameChain, (name |> Option.ofObj |> Option.defaultValue "<null>"))
      validationBindingsByName
      |> IReadOnlyDictionary.tryFind name
      |> Option.map (fun b -> !b.Errors)
      |> Option.defaultValue []
      |> (fun x -> upcast x)