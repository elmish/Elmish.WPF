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

type GetErrorSubModelSelectedItem =
  { NameChain: string
    SubModelSeqBindingName: string
    Id: string }

[<RequireQualifiedAccess>]
type internal GetError =
  | OneWayToSource
  | SubModelSelectedItem of GetErrorSubModelSelectedItem


type internal OneWayBinding<'model> = {
  OneWayData: OneWayData<'model>
}

type internal OneWayToSourceBinding<'model> = {
  Set: obj -> 'model -> unit
}

type internal OneWaySeqBinding<'model, 'a, 'b, 'id when 'id : equality> = {
  OneWaySeqData: OneWaySeqLazyData<'model, 'a, 'b, 'id> // TODO: consider renaming so that both contain "Lazy" or neither do
  Values: ObservableCollection<'b>
}

type internal TwoWayBinding<'model> = {
  Get: 'model -> obj
  Set: obj -> 'model -> unit
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

and internal SubModelSeqUnkeyedBinding<'model, 'msg, 'bindingModel, 'bindingMsg> = {
  SubModelSeqUnkeyedData: SubModelSeqUnkeyedData<'model, 'msg, 'bindingModel, 'bindingMsg>
  Vms: ObservableCollection<ViewModel<'bindingModel, 'bindingMsg>>
}

and internal SubModelSeqKeyedBinding<'model, 'msg, 'bindingModel, 'bindingMsg, 'id when 'id : equality> = {
  SubModelSeqKeyedData: SubModelSeqKeyedData<'model, 'msg, 'bindingModel, 'bindingMsg, 'id>
  Vms: ObservableCollection<ViewModel<'bindingModel, 'bindingMsg>>
}

and internal SubModelSelectedItemBinding<'model, 'msg, 'bindingModel, 'bindingMsg, 'id when 'id : equality> =
  { Get: 'model -> 'id voption
    Set: 'id voption -> 'model -> unit
    SubModelSeqBindingName: string
    SubModelSeqKeyedBinding: SubModelSeqKeyedBinding<'model, 'msg, 'bindingModel, 'bindingMsg, 'id> }

  member d.DidPropertyChange(currentModel: 'model, newModel: 'model) =
    d.Get currentModel <> d.Get newModel

  member d.TryGetMember
      (getBindingModel: 'vm -> 'bindingModel,
       subModelSeqKeyedData: SubModelSeqKeyedData<'model, 'msg, 'bindingModel, 'bindingMsg, 'id>,
       viewModels: ObservableCollection<'vm>,
       model: 'model) =
    d.Get model
    |> ValueOption.map (fun selectedId ->
      selectedId,
      viewModels
      |> Seq.tryFind (getBindingModel >> subModelSeqKeyedData.GetId >> (=) selectedId))

  member d.TrySetMember
      (subModelSeqData: SubModelSeqKeyedData<'model, 'msg, 'bindingModel, 'bindingMsg, 'id>,
       model: 'model,
       bindingModel: 'bindingModel voption) =
    let id = bindingModel |> ValueOption.map subModelSeqData.GetId
    d.Set id model


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


and internal BaseVmBinding<'model, 'msg> =
  | OneWay of OneWayBinding<'model>
  | OneWayToSource of OneWayToSourceBinding<'model>
  | OneWaySeq of OneWaySeqBinding<'model, obj, obj, obj>
  | TwoWay of TwoWayBinding<'model>
  | Cmd of cmd: Command
  | SubModel of SubModelBinding<'model, 'msg, obj, obj>
  | SubModelWin of SubModelWinBinding<'model, 'msg, obj, obj>
  | SubModelSeqUnkeyed of SubModelSeqUnkeyedBinding<'model, 'msg, obj, obj>
  | SubModelSeqKeyed of SubModelSeqKeyedBinding<'model, 'msg, obj, obj, obj>
  | SubModelSelectedItem of SubModelSelectedItemBinding<'model, 'msg, obj, obj, obj>

/// Represents all necessary data used in an active binding.
and internal VmBinding<'model, 'msg> =
  | BaseVmBinding of BaseVmBinding<'model, 'msg>
  | Cached of CachedBinding<'model, 'msg, obj>
  | Validatation of ValidationBinding<'model, 'msg>
  | Lazy of LazyBinding<'model, 'msg>

  with
    member this.AddCaching = Cached { Binding = this; Cache = ref None }
    member this.AddLazy equals = { Binding = this; Equals = equals } |> Lazy
    member this.AddValidation currentModel validate =
      { Binding = this
        Validate = validate
        Errors = currentModel |> validate |> ref }
      |> Validatation

    member this.BaseCase =
      match this with
      | BaseVmBinding b -> b
      | Cached b -> b.Binding.BaseCase
      | Validatation b -> b.Binding.BaseCase
      | Lazy b -> b.Binding.BaseCase

    member this.FirstValidation =
      match this with
      | BaseVmBinding _ -> None
      | Cached b -> b.Binding.FirstValidation
      | Lazy b -> b.Binding.FirstValidation
      | Validatation b -> b |> Some // TODO: what if there is more than one validation effect?


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

  let initializeBinding name getInitializedBindingByName =
    let measure x = x |> measure name
    let measure2 x = x |> measure2 name
    let baseCase dispatch = function
      | OneWayData d ->
          { OneWayData = d |> BindingData.OneWay.measureFunctions measure }
          |> OneWay
          |> BaseVmBinding
          |> Some
      | OneWayToSourceData d ->
          let d = d |> BindingData.OneWayToSource.measureFunctions measure
          { Set = fun obj m -> d.Set obj m |> dispatch }
          |> OneWayToSource
          |> BaseVmBinding
          |> Some
      | OneWaySeqLazyData d ->
          { OneWaySeqData = d |> BindingData.OneWaySeqLazy.measureFunctions measure measure measure2 measure measure2
            Values = ObservableCollection(initialModel |> d.Get |> d.Map) }
          |> OneWaySeq
          |> BaseVmBinding
          |> Some
      | TwoWayData d ->
          let d = d |> BindingData.TwoWay.measureFunctions measure measure
          { Get = d.Get
            Set = fun obj m -> d.Set obj m |> dispatch }
          |> TwoWay
          |> BaseVmBinding
          |> Some
      | CmdData d ->
          let d = d |> BindingData.Cmd.measureFunctions measure2 measure2
          let execute param = d.Exec param currentModel |> ValueOption.iter dispatch
          let canExecute param = d.CanExec param currentModel
          let cmd = Command(execute, canExecute)
          if d.AutoRequery then
            cmd.AddRequeryHandler ()
          cmd
          |> Cmd
          |> BaseVmBinding
          |> Some
      | SubModelData d ->
          let d = d |> BindingData.SubModel.measureFunctions measure measure measure2
          let toMsg = fun msg -> d.ToMsg currentModel msg
          d.GetModel initialModel
          |> ValueOption.map (fun m -> ViewModel(m, toMsg >> dispatch, d.GetBindings (), performanceLogThresholdMs, getNameChainFor name, log, logPerformance))
          |> (fun vm -> { SubModelData = d; Vm = ref vm })
          |> SubModel
          |> BaseVmBinding
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
          |> BaseVmBinding
          |> Some
      | SubModelSeqUnkeyedData d ->
          let d = d |> BindingData.SubModelSeqUnkeyed.measureFunctions measure measure measure2
          let toMsg = fun msg -> d.ToMsg currentModel msg
          let vms =
            d.GetModels initialModel
            |> Seq.indexed
            |> Seq.map (fun (idx, m) ->
                 let chain = getNameChainForItem name (idx |> string)
                 ViewModel(m, (fun msg -> toMsg (idx, msg) |> dispatch), d.GetBindings (), performanceLogThresholdMs, chain, log, logPerformance)
            )
            |> ObservableCollection
          { SubModelSeqUnkeyedData = d
            Vms = vms }
          |> SubModelSeqUnkeyed
          |> BaseVmBinding
          |> Some
      | SubModelSeqKeyedData d ->
          let d = d |> BindingData.SubModelSeqKeyed.measureFunctions measure measure measure2 measure
          let toMsg = fun msg -> d.ToMsg currentModel msg
          let vms =
            d.GetSubModels initialModel
            |> Seq.map (fun m ->
                 let mId = d.GetId m
                 let chain = getNameChainForItem name (mId |> string)
                 ViewModel(m, (fun msg -> toMsg (mId, msg) |> dispatch), d.GetBindings (), performanceLogThresholdMs, chain, log, logPerformance)
            )
            |> ObservableCollection
          { SubModelSeqKeyedData = d
            Vms = vms }
          |> SubModelSeqKeyed
          |> BaseVmBinding
          |> Some
      | SubModelSelectedItemData d ->
          let d = d |> BindingData.SubModelSelectedItem.measureFunctions measure measure2
          match getInitializedBindingByName d.SubModelSeqBindingName with
          | Some (SubModelSeqKeyed b) ->
              { Get = d.Get
                Set = fun obj m -> d.Set obj m |> dispatch
                SubModelSeqBindingName = d.SubModelSeqBindingName
                SubModelSeqKeyedBinding = b }
              |> SubModelSelectedItem
              |> BaseVmBinding
              |> (fun b -> b.AddCaching)
              |> Some
          | Some _ ->
              log.LogError("SubModelSelectedItem binding referenced binding {SubModelSeqBindingName} but it is not a SubModelSeq binding", d.SubModelSeqBindingName)
              None
          | None ->
              log.LogError("SubModelSelectedItem binding referenced binding {SubModelSeqBindingName} but no binding was found with that name", d.SubModelSeqBindingName)
              None
    let rec recursiveCase dispatch = function
      | BaseBindingData d -> d |> baseCase (box >> dispatch)
      | CachingData d ->
          d
          |> recursiveCase dispatch
          |> Option.map (fun b -> b.AddCaching)
      | ValidationData d ->
          let d = d |> BindingData.Validation.measureFunctions measure
          d.BindingData
          |> recursiveCase dispatch
          |> Option.map (fun b -> b.AddValidation currentModel d.Validate)
      | LazyData d ->
          let d = d |> BindingData.Lazy.measureFunctions measure
          d.BindingData
          |> recursiveCase dispatch
          |> Option.map (fun b -> b.AddLazy d.Equals)
      | WrapDispatchData d ->
          d.BindingData
          |> recursiveCase (d.WrapDispatch dispatch)
    recursiveCase

  let (bindings, validationBindings) =
    log.LogTrace("[{BindingNameChain}] Initializing bindings", nameChain)
    let bindingDict = Dictionary<string, VmBinding<'model, 'msg>>(bindings.Length)
    let bindingDictAsFunc = flip Dictionary.tryFind bindingDict >> Option.map (fun x -> x.BaseCase)
    let sortedBindings = bindings |> List.sortWith Binding.subModelSelectedItemLast
    for b in sortedBindings do
      if bindingDict.ContainsKey b.Name then
        log.LogError("Binding name {BindingName} is duplicated. Only the first occurrence will be used.", b.Name)
      else
        initializeBinding b.Name bindingDictAsFunc (unbox >> dispatch) b.Data
        |> Option.iter (fun binding ->
          bindingDict.Add(b.Name, binding))
    let validationDict = Dictionary<string, ValidationBinding<'model, 'msg>>()
    bindingDict
    |> Seq.map (Pair.ofKvp >> Pair.mapAll Some (fun x -> x.FirstValidation) >> PairOption.sequence)
    |> SeqOption.somes
    |> Seq.iter validationDict.Add
    (bindingDict    :> IReadOnlyDictionary<_,_>,
     validationDict :> IReadOnlyDictionary<_,_>)

  /// Updates the binding and returns a list indicating what events to raise
  /// for this binding
  let updateBinding name newModel =
    let baseCase = function
      | OneWay _
      | TwoWay _ -> [ PropertyChanged ]
      | OneWayToSource _ -> []
      | OneWaySeq b ->
          b.OneWaySeqData.Merge(b.Values, currentModel, newModel)
          []
      | Cmd cmd -> cmd |> CanExecuteChanged |> List.singleton
      | SubModel b ->
        let d = b.SubModelData
        match !b.Vm, d.GetModel newModel with
        | ValueNone, ValueNone -> []
        | ValueSome _, ValueNone ->
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
                 * so instead of immediately executing Window.Close via Dispatcher.Invoke,
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
      | SubModelSeqUnkeyed b ->
          let d = b.SubModelSeqUnkeyedData
          let create m idx =
            let toMsg = fun msg -> d.ToMsg currentModel msg
            let chain = getNameChainForItem name (idx |> string)
            ViewModel(m, (fun msg -> toMsg (idx, msg) |> dispatch), d.GetBindings (), performanceLogThresholdMs, chain, log, logPerformance)
          let update (vm: ViewModel<_, _>) = vm.UpdateModel
          Merge.unkeyed create update b.Vms (d.GetModels newModel)
          []
      | SubModelSeqKeyed b ->
          let d = b.SubModelSeqKeyedData
          let getTargetId getId (vm: ViewModel<_, _>) = getId vm.CurrentModel
          let create m id =
            let toMsg = fun msg -> d.ToMsg currentModel msg
            let chain = getNameChainForItem name (id |> string)
            ViewModel(m, (fun msg -> toMsg (id, msg) |> dispatch), d.GetBindings (), performanceLogThresholdMs, chain, log, logPerformance)
          let update (vm: ViewModel<_, _>) = vm.UpdateModel
          let newSubModels = newModel |> d.GetSubModels |> Seq.toArray
          try
            d.MergeKeyed(getTargetId, create, update, b.Vms, newSubModels)
          with
            | :? Elmish.WPF.DuplicateIdException as e ->
              let messageTemplate = "[{BindingNameChain}] In the {SourceOrTarget} sequence of the binding {BindingName}, the elements at indices {Index1} and {Index2} have the same ID {ID}. To avoid this problem, the elements will be merged without using IDs."
              log.LogError(messageTemplate, nameChain, e.SourceOrTarget, name, e.Index1, e.Index2, e.Id)
              let create m _ = create m (d.GetId m)
              Merge.unkeyed create update b.Vms newSubModels
          []
      | SubModelSelectedItem b ->
          b.DidPropertyChange(currentModel, newModel)
          |> Option.fromBool PropertyChanged
          |> Option.toList
    let rec recursiveCase = function
      | BaseVmBinding b -> b |> baseCase
      | Cached b ->
          let updates = recursiveCase b.Binding
          updates
          |> List.filter ((=) PropertyChanged)
          |> List.iter (fun _ -> b.Cache := None)
          updates
      | Validatation b ->
          let updates = recursiveCase b.Binding
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
            recursiveCase b.Binding
    recursiveCase

  let tryGetMember model =
    let baseCase = function
      | OneWay { OneWayData = d } -> d.Get model |> Ok
      | TwoWay b -> b.Get model |> Ok
      | OneWayToSource _ -> GetError.OneWayToSource |> Error
      | OneWaySeq { Values = vals } -> vals |> box |> Ok
      | Cmd cmd -> cmd |> box |> Ok
      | SubModel { Vm = vm } -> !vm |> ValueOption.toObj |> box |> Ok
      | SubModelWin { VmWinState = vm } ->
          !vm
          |> WindowState.toVOption
          |> ValueOption.map box
          |> ValueOption.toObj
          |> Ok
      | SubModelSeqUnkeyed { Vms = vms }
      | SubModelSeqKeyed { Vms = vms } -> vms |> box |> Ok
      | SubModelSelectedItem b ->
          b.TryGetMember
            ((fun (vm: ViewModel<_, _>) -> vm.CurrentModel),
             b.SubModelSeqKeyedBinding.SubModelSeqKeyedData,
             b.SubModelSeqKeyedBinding.Vms,
             model)
          |> function
            | ValueNone -> ValueNone |> Ok // deselecting successful
            | ValueSome (id, mVm) ->
                match mVm with
                | Some vm -> (id, vm) |> ValueSome |> Ok // selecting successful
                | None -> // selecting failed
                    { NameChain = nameChain
                      SubModelSeqBindingName = b.SubModelSeqBindingName
                      Id = id.ToString() }
                    |> GetError.SubModelSelectedItem
                    |> Error
          |> Result.map (ValueOption.map snd >> ValueOption.toObj >> box)
    let rec recursiveCase = function
      | BaseVmBinding b -> b |> baseCase
      | Cached b ->
          match !b.Cache with
          | Some v -> v |> Ok
          | None ->
              let x = recursiveCase b.Binding
              x |> Result.iter (fun v -> b.Cache := Some v)
              x
      | Validatation b -> recursiveCase b.Binding
      | Lazy b -> recursiveCase b.Binding
    recursiveCase

  let trySetMember model (value: obj) =
    let baseCase = function // TOOD: return 'msg option
      | TwoWay b ->
          b.Set value model
          true
      | OneWayToSource b ->
          b.Set value model
          true
      | SubModelSelectedItem b ->
          let bindingModel =
            (value :?> ViewModel<obj, obj>)
            |> ValueOption.ofObj
            |> ValueOption.map (fun vm -> vm.CurrentModel)
          b.TrySetMember(b.SubModelSeqKeyedBinding.SubModelSeqKeyedData, model, bindingModel)
          true
      | OneWay _
      | OneWaySeq _
      | Cmd _
      | SubModel _
      | SubModelWin _
      | SubModelSeqUnkeyed _
      | SubModelSeqKeyed _ ->
          false
    let rec resursiveCase = function
      | BaseVmBinding b -> b |> baseCase
      | Cached b ->
          let successful = resursiveCase b.Binding
          if successful then
            b.Cache := None  // TODO #185: write test
          successful
      | Validatation b -> resursiveCase b.Binding
      | Lazy b -> resursiveCase b.Binding
    resursiveCase

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
        try
          match tryGetMember currentModel binding with
          | Ok v ->
              result <- v
              true
          | Error e ->
              match e with
              | GetError.OneWayToSource -> log.LogError("[{BindingNameChain}] TryGetMember FAILED: Binding {BindingName} is read-only", nameChain, binder.Name)
              | GetError.SubModelSelectedItem d ->
                  log.LogError(
                    "[{BindingNameChain}] TryGetMember FAILED: Failed to find an element of the SubModelSeq binding {SubModelSeqBindingName} with ID {ID} in the getter for the binding {SubModelSelectedItemName}",
                    d.NameChain,
                    d.SubModelSeqBindingName,
                    d.Id)
              false
        with e ->
          log.LogError(e, "[{BindingNameChain}] TryGetMember FAILED: Exception thrown while processing binding {BindingName}", nameChain, binder.Name)
          reraise ()

  override _.TrySetMember (binder, value) =
    log.LogTrace("[{BindingNameChain}] TrySetMember {BindingName}", nameChain, binder.Name)
    match bindings.TryGetValue binder.Name with
    | false, _ ->
        log.LogError("[{BindingNameChain}] TrySetMember FAILED: Property {BindingName} doesn't exist", nameChain, binder.Name)
        false
    | true, binding ->
        try
          let success = trySetMember currentModel value binding
          if not success then
            log.LogError("[{BindingNameChain}] TrySetMember FAILED: Binding {BindingName} is read-only", nameChain, binder.Name)
          success
        with e ->
          log.LogError(e, "[{BindingNameChain}] TrySetMember FAILED: Exception thrown while processing binding {BindingName}", nameChain, binder.Name)
          reraise ()


  interface INotifyPropertyChanged with
    [<CLIEvent>]
    member _.PropertyChanged = propertyChanged.Publish

  interface INotifyDataErrorInfo with
    [<CLIEvent>]
    member _.ErrorsChanged = errorsChanged.Publish
    member _.HasErrors =
      // WPF calls this too often, so don't log https://github.com/elmish/Elmish.WPF/issues/354
      validationBindings
      |> Seq.map (fun (Kvp(_, b)) -> !b.Errors)
      |> Seq.filter (not << List.isEmpty)
      |> (not << Seq.isEmpty)
    member _.GetErrors name =
      let name = name |> Option.ofObj |> Option.defaultValue "<null>" // entity-level errors are being requested when given null or ""  https://docs.microsoft.com/en-us/dotnet/api/system.componentmodel.inotifydataerrorinfo.geterrors#:~:text=null%20or%20Empty%2C%20to%20retrieve%20entity-level%20errors
      log.LogTrace("[{BindingNameChain}] GetErrors {BindingName}", nameChain, name)
      validationBindings
      |> IReadOnlyDictionary.tryFind name
      |> Option.map (fun b -> !b.Errors)
      |> Option.defaultValue []
      |> (fun x -> upcast x)