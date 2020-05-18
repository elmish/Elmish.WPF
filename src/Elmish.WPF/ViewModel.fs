namespace Elmish.WPF

open System
open System.Dynamic
open System.Collections.Generic
open System.Collections.ObjectModel
open System.ComponentModel
open System.Windows

open Elmish


type internal OneWayBinding<'model, 'a> = {
  Get: 'model -> 'a
}

type internal OneWayLazyBinding<'model, 'a, 'b> = {
  Get: 'model -> 'a
  Equals: 'a -> 'a -> bool
  Map: 'a -> 'b
}

type internal OneWaySeqBinding<'model, 'a, 'b, 'id> = {
  Get: 'model -> 'a
  Equals: 'a -> 'a -> bool
  Map: 'a -> 'b seq
  GetId: 'b -> 'id
  ItemEquals: 'b -> 'b -> bool
  Values: ObservableCollection<'b>
}

type internal TwoWayBinding<'model, 'msg, 'a> = {
  Get: 'model -> 'a
  Set: 'a -> 'model -> unit
}

type internal TwoWayValidateBinding<'model, 'msg, 'a> = {
  Get: 'model -> 'a
  Set: 'a -> 'model -> unit
  Validate: 'model -> string voption
}

type internal CmdBinding<'model, 'msg> = {
  Cmd: Command
  CanExec: 'model -> bool
}

type internal SubModelBinding<'model, 'msg, 'bindingModel, 'bindingMsg> = {
  GetModel: 'model -> 'bindingModel voption
  GetBindings: unit -> Binding<'bindingModel, 'bindingMsg> list
  ToMsg: 'bindingMsg -> 'msg
  Sticky: bool
  Vm: ViewModel<'bindingModel, 'bindingMsg> voption ref
}

and internal SubModelWinBinding<'model, 'msg, 'bindingModel, 'bindingMsg> = {
  GetState: 'model -> WindowState<'bindingModel>
  GetBindings: unit -> Binding<'bindingModel, 'bindingMsg> list
  ToMsg: 'bindingMsg -> 'msg
  GetWindow: 'model -> Dispatch<'msg> -> Window
  IsModal: bool
  OnCloseRequested: unit -> unit
  WinRef: WeakReference<Window>
  PreventClose: bool ref
  VmWinState: WindowState<ViewModel<'bindingModel, 'bindingMsg>> ref
}

and internal SubModelSeqBinding<'model, 'msg, 'bindingModel, 'bindingMsg, 'id> = {
  GetModels: 'model -> 'bindingModel seq
  GetId: 'bindingModel -> 'id
  GetBindings: unit -> Binding<'bindingModel, 'bindingMsg> list
  ToMsg: 'id * 'bindingMsg -> 'msg
  Vms: ObservableCollection<ViewModel<'bindingModel, 'bindingMsg>>
}

and internal SubModelSelectedItemBinding<'model, 'msg, 'bindingModel, 'bindingMsg, 'id> = {
  Get: 'model -> 'id voption
  Set: 'id voption -> 'model -> unit
  SubModelSeqBinding: SubModelSeqBinding<'model, 'msg, obj, obj, obj>
}

and internal CachedBinding<'model, 'msg, 'value> = {
  Binding: VmBinding<'model, 'msg>
  Cache: 'value option ref
}


/// Represents all necessary data used in an active binding.
and internal VmBinding<'model, 'msg> =
  | OneWay of OneWayBinding<'model, obj>
  | OneWayLazy of OneWayLazyBinding<'model, obj, obj>
  | OneWaySeq of OneWaySeqBinding<'model, obj, obj, obj>
  | TwoWay of TwoWayBinding<'model, 'msg, obj>
  | TwoWayValidate of TwoWayValidateBinding<'model, 'msg, obj>
  | Cmd of CmdBinding<'model, 'msg>
  | CmdParam of cmd: Command
  | SubModel of SubModelBinding<'model, 'msg, obj, obj>
  | SubModelWin of SubModelWinBinding<'model, 'msg, obj, obj>
  | SubModelSeq of SubModelSeqBinding<'model, 'msg, obj, obj, obj>
  | SubModelSelectedItem of SubModelSelectedItemBinding<'model, 'msg, obj, obj, obj>
  | Cached of CachedBinding<'model, 'msg, obj>


and [<AllowNullLiteral>] internal ViewModel<'model, 'msg>
      ( initialModel: 'model,
        dispatch: 'msg -> unit,
        bindings: Binding<'model, 'msg> list,
        config: ElmConfig,
        propNameChain: string)
      as this =
  inherit DynamicObject()

  let mutable currentModel = initialModel

  let propertyChanged = Event<PropertyChangedEventHandler, PropertyChangedEventArgs>()
  let errorsChanged = DelegateEvent<EventHandler<DataErrorsChangedEventArgs>>()

  /// Error messages keyed by property name.
  let errors = Dictionary<string, string>()


  let withCaching b = Cached { Binding = b; Cache = ref None }


  let log fmt =
    let innerLog (str: string) =
      if config.LogConsole then Console.WriteLine(str)
      if config.LogTrace then Diagnostics.Trace.WriteLine(str)
    Printf.kprintf innerLog fmt

  let logInvalidGetId = log "The getId function must return distinct IDs, but it returned the same ID %A for %A and %A"

  let getPropChainFor bindingName =
    sprintf "%s.%s" propNameChain bindingName

  let getPropChainForItem collectionBindingName itemId =
    sprintf "%s.%s.%s" propNameChain collectionBindingName itemId

  let notifyPropertyChanged propName =
    log "[%s] PropertyChanged \"%s\"" propNameChain propName
    propertyChanged.Trigger(this, PropertyChangedEventArgs propName)

  let raiseCanExecuteChanged (cmd: Command) =
    cmd.RaiseCanExecuteChanged ()

  let setError error propName =
    match errors.TryGetValue propName with
    | true, err when err = error -> ()
    | _ ->
        log "[%s] ErrorsChanged \"%s\"" propNameChain propName
        errors.[propName] <- error
        errorsChanged.Trigger([| box this; box <| DataErrorsChangedEventArgs propName |])

  let removeError propName =
    if errors.Remove propName then
      log "[%s] ErrorsChanged \"%s\"" propNameChain propName
      errorsChanged.Trigger([| box this; box <| DataErrorsChangedEventArgs propName |])

  let rec updateValidationError model name = function
    | TwoWayValidate { Validate = validate } ->
        match validate model with
        | ValueNone -> removeError name
        | ValueSome error -> setError error name
    | OneWay _
    | OneWayLazy _
    | OneWaySeq _
    | TwoWay _
    | Cmd _
    | CmdParam _
    | SubModel _
    | SubModelWin _
    | SubModelSeq _
    | SubModelSelectedItem _ -> ()
    | Cached b -> updateValidationError model name b.Binding

  let measure name callName f =
    if not config.Measure then f
    else
      fun x ->
        let sw = System.Diagnostics.Stopwatch.StartNew ()
        let r = f x
        sw.Stop ()
        if sw.ElapsedMilliseconds >= int64 config.MeasureLimitMs then
          log "[%s] %s (%ims): %s" propNameChain callName sw.ElapsedMilliseconds name
        r

  let measure2 name callName f =
    if not config.Measure then f
    else fun x -> measure name callName (f x)

  let showNewWindow
      (winRef: WeakReference<Window>)
      (getWindow: 'model -> Dispatch<'msg> -> Window)
      dataContext
      isDialog
      (onCloseRequested: unit -> unit)
      (preventClose: bool ref)
      initialVisibility =
    let win = getWindow currentModel dispatch
    winRef.SetTarget win
    win.Dispatcher.Invoke(fun () ->
      let guiCtx = System.Threading.SynchronizationContext.Current
      async {
        win.DataContext <- dataContext
        win.Closing.Add(fun ev ->
          ev.Cancel <- !preventClose
          async {
            do! Async.SwitchToThreadPool()
            onCloseRequested ()
          } |> Async.StartImmediate
        )
        do! Async.SwitchToContext guiCtx
        if isDialog
        then win.ShowDialog () |> ignore
        else win.Visibility <- initialVisibility
      } |> Async.StartImmediate
    )

  let initializeBinding name bindingData getInitializedBindingByName =
    match bindingData with
    | OneWayData d ->
        Some <| OneWay {
          Get = measure name "get" d.Get }
    | OneWayLazyData d ->
        let get = measure name "get" d.Get
        let map = measure name "map" d.Map
        OneWayLazy {
          Get = get
          Map = map
          Equals = measure2 name "equals" d.Equals
        } |> withCaching |> Some
    | OneWaySeqLazyData d ->
        let get = measure name "get" d.Get
        let map = measure name "map" d.Map
        let getId = measure name "getId" d.GetId
        let values = ObservableCollection(initialModel |> get |> map)
        let valuesById = Dictionary<_,_>(values.Count)
        for value in values do
          let id = getId value
          if valuesById.ContainsKey id
          then logInvalidGetId id (valuesById.[id]) value
          else valuesById.Add(id, value)
        Some <| OneWaySeq {
          Get = get
          Map = map
          Equals = measure2 name "equals" d.Equals
          GetId = getId
          ItemEquals = measure2 name "itemEquals" d.ItemEquals
          Values = values }
    | TwoWayData d ->
        let set = measure2 name "set" d.Set
        let dispatch' = d.WrapDispatch dispatch
        Some <| TwoWay {
          Get = measure name "get" d.Get
          Set = fun obj m -> set obj m |> dispatch' }
    | TwoWayValidateData d ->
        let set = measure2 name "set" d.Set
        let dispatch' = d.WrapDispatch dispatch
        Some <| TwoWayValidate {
          Get = measure name "get" d.Get
          Set = fun obj m -> set obj m |> dispatch'
          Validate = measure name "validate" d.Validate }
    | CmdData d ->
        let exec = measure name "exec" d.Exec
        let canExec = measure name "canExec" d.CanExec
        let dispatch' = d.WrapDispatch dispatch
        let execute _ = exec currentModel |> ValueOption.iter dispatch'
        let canExecute _ = canExec currentModel
        Some <| Cmd {
          Cmd = Command(execute, canExecute, false)
          CanExec = canExec }
    | CmdParamData d ->
        let exec = measure2 name "exec" d.Exec
        let canExec = measure2 name "canExec" d.CanExec
        let dispatch' = d.WrapDispatch dispatch
        let execute param = exec param currentModel |> ValueOption.iter dispatch'
        let canExecute param = canExec param currentModel
        Some <| CmdParam (Command(execute, canExecute, d.AutoRequery))
    | SubModelData d ->
        let getModel = measure name "getSubModel" d.GetModel
        let getBindings = measure name "bindings" d.GetBindings
        let toMsg = measure name "toMsg" d.ToMsg
        match getModel initialModel with
        | ValueNone ->
            Some <| SubModel {
              GetModel = getModel
              GetBindings = getBindings
              ToMsg = toMsg
              Sticky = d.Sticky
              Vm = ref ValueNone }
        | ValueSome m ->
            let chain = getPropChainFor name
            let vm = ViewModel(m, toMsg >> dispatch, getBindings (), config, chain)
            Some <| SubModel {
              GetModel = getModel
              GetBindings = getBindings
              ToMsg = toMsg
              Sticky = d.Sticky
              Vm = ref <| ValueSome vm }
    | SubModelWinData d ->
        let getState = measure name "getState" d.GetState
        let getBindings = measure name "bindings" d.GetBindings
        let toMsg = measure name "toMsg" d.ToMsg
        let onCloseRequested = fun () -> d.OnCloseRequested |> ValueOption.iter dispatch
        match getState initialModel with
        | WindowState.Closed ->
            Some <| SubModelWin {
              GetState = getState
              GetBindings = getBindings
              ToMsg = toMsg
              GetWindow = d.GetWindow
              IsModal = d.IsModal
              OnCloseRequested = onCloseRequested
              WinRef = WeakReference<_>(null)
              PreventClose = ref true
              VmWinState = ref WindowState.Closed
            }
        | WindowState.Hidden m ->
            let chain = getPropChainFor name
            let vm = ViewModel(m, toMsg >> dispatch, getBindings (), config, chain)
            let winRef = WeakReference<_>(null)
            let preventClose = ref true
            log "[%s] Creating hidden window" chain
            showNewWindow
              winRef d.GetWindow vm d.IsModal onCloseRequested
              preventClose Visibility.Hidden
            Some <| SubModelWin {
              GetState = getState
              GetBindings = getBindings
              ToMsg = toMsg
              GetWindow = d.GetWindow
              IsModal = d.IsModal
              OnCloseRequested = onCloseRequested
              WinRef = winRef
              PreventClose = preventClose
              VmWinState = ref <| WindowState.Hidden vm
            }
        | WindowState.Visible m ->
            let chain = getPropChainFor name
            let vm = ViewModel(m, toMsg >> dispatch, getBindings (), config, chain)
            let winRef = WeakReference<_>(null)
            let preventClose = ref true
            log "[%s] Creating and opening window" chain
            showNewWindow
              winRef d.GetWindow vm d.IsModal onCloseRequested
              preventClose Visibility.Visible
            Some <| SubModelWin {
              GetState = getState
              GetBindings = getBindings
              ToMsg = toMsg
              GetWindow = d.GetWindow
              IsModal = d.IsModal
              OnCloseRequested = onCloseRequested
              WinRef = winRef
              PreventClose = preventClose
              VmWinState = ref <| WindowState.Visible vm
            }
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
        let modelsById = Dictionary<_,_>(vms.Count)
        for model in vms |> Seq.map (fun vm -> vm.CurrentModel) do
          let id = getId model
          if modelsById.ContainsKey id
          then logInvalidGetId id (modelsById.[id]) model
          else modelsById.Add(id, model)
        Some <| SubModelSeq {
          GetModels = getModels
          GetId = getId
          GetBindings = getBindings
          ToMsg = toMsg
          Vms = vms }
    | SubModelSelectedItemData d ->
        match getInitializedBindingByName d.SubModelSeqBindingName with
        | Some (SubModelSeq b) ->
          let get = measure name "get" d.Get
          let set = measure2 name "set" d.Set
          let dispatch' = d.WrapDispatch dispatch
          SubModelSelectedItem {
            Get = get
            Set = fun obj m -> set obj m |> dispatch'
            SubModelSeqBinding = b
          } |> withCaching |> Some
        | _ ->
          log "subModelSelectedItem binding referenced binding '%s', but no compatible binding was found with that name" d.SubModelSeqBindingName
          None

  let bindings =
    log "[%s] Initializing bindings" propNameChain
    let dict = Dictionary<string, VmBinding<'model, 'msg>>(bindings.Length)
    let dictAsFunc name =
      match dict.TryGetValue name with
      | true, b -> Some b
      | _ -> None
    let sortedBindings = bindings |> List.sortWith BindingData.subModelSelectedItemLast
    for b in sortedBindings do
      if dict.ContainsKey b.Name then
        log "Binding name '%s' is duplicated. Only the first occurance will be used." b.Name
      else
        initializeBinding b.Name b.Data dictAsFunc
        |> Option.iter (fun binding ->
          dict.Add(b.Name, binding)
          updateValidationError initialModel b.Name binding)
    dict :> IReadOnlyDictionary<string, VmBinding<'model, 'msg>>

  let oneWaySeqMerge
      logInvalidGetSourceId
      logInvalidGetTargetId
      getSourceId
      getTargetId
      create
      update
      (target: ObservableCollection<_>)
      (source: _ array) =
    let sourceIdxItemPairsById = Dictionary<_,_>(source.Length)
    for (idx, s) in source |> Seq.indexed do
      let id = getSourceId s
      if sourceIdxItemPairsById.ContainsKey id
      then logInvalidGetSourceId id (sourceIdxItemPairsById.[id]) s
      else sourceIdxItemPairsById.Add(id, (idx, s))

    let targetIdxItemPairsById = Dictionary<_,_>(target.Count)
    for (idx, t) in target |> Seq.indexed do
      let id = getTargetId t
      if targetIdxItemPairsById.ContainsKey id
      then logInvalidGetTargetId id (targetIdxItemPairsById.[id]) t
      else targetIdxItemPairsById.Add(id, (idx, t))

    if sourceIdxItemPairsById.Count = source.Length && targetIdxItemPairsById.Count = target.Count then
      // Update existing values
      for Kvp (tId, (tIdx, t)) in targetIdxItemPairsById do
        match sourceIdxItemPairsById.TryGetValue tId with
        | true, (_, s) -> update t s tIdx
        | _ -> ()
      
      // Remove old values that no longer exist
      if target.Count <> 0 && source.Length = 0
      then target.Clear ()
      else
        for tIdx in target.Count - 1..-1..0 do
          let tId = getTargetId target.[tIdx]
          if tId |> sourceIdxItemPairsById.ContainsKey |> not then
            let (tIdx2, _) = targetIdxItemPairsById.[tId] // tIdx = tIdx2, so this line is unnecessary
            target.RemoveAt tIdx2
      
      // Add new values that don't currently exist
      let create (Kvp (sId, (_, s))) = create s sId
      sourceIdxItemPairsById
      |> Seq.filter (Kvp.key >> targetIdxItemPairsById.ContainsKey >> not)
      |> Seq.map create
      |> Seq.iter target.Add
      
      // Reorder according to new model list
      for Kvp (sId, (sIdx, _)) in sourceIdxItemPairsById do
        let tIdx =
          target
          |> Seq.indexed
          |> Seq.find (fun (_, t) -> getTargetId t = sId)
          |> fst
        if tIdx <> sIdx then target.Move(tIdx, sIdx)

  let subModelSeqMerge
      logInvalidGetSourceId
      logInvalidGetTargetId
      getSourceId
      getTargetId
      create
      update
      (target: ObservableCollection<_>)
      (source: _ array) =
    let sourceIdxItemPairsById = Dictionary<_,_>(source.Length)
    for (idx, s) in source |> Seq.indexed do
      let id = getSourceId s
      if sourceIdxItemPairsById.ContainsKey id
      then logInvalidGetSourceId id (sourceIdxItemPairsById.[id]) s
      else sourceIdxItemPairsById.Add(id, (idx, s))

    let targetIdxItemPairsById = Dictionary<_,_>(target.Count)
    for (idx, t) in target |> Seq.indexed do
      let id = getTargetId t
      if targetIdxItemPairsById.ContainsKey id
      then logInvalidGetTargetId id (targetIdxItemPairsById.[id]) t
      else targetIdxItemPairsById.Add(id, (idx, t))

    if sourceIdxItemPairsById.Count = source.Length && targetIdxItemPairsById.Count = target.Count then
      // Update existing models
      for Kvp (tId, (tIdx, t)) in targetIdxItemPairsById do
        match sourceIdxItemPairsById.TryGetValue tId with
        | true, (_, s) -> update t s tIdx
        | _ -> ()
      
      // Remove old view models that no longer exist
      if target.Count <> 0 && source.Length = 0
      then target.Clear ()
      else
        for tIdx in target.Count - 1..-1..0 do
          let tId = getTargetId target.[tIdx]
          if tId |> sourceIdxItemPairsById.ContainsKey |> not then
            let (tIdx2, _) = targetIdxItemPairsById.[tId] // tIdx = tIdx2, so this line is unnecessary
            target.RemoveAt tIdx2
      
      // Add new models that don't currently exist
      let create (Kvp (sId, (_, s))) = create s sId
      sourceIdxItemPairsById
      |> Seq.filter (Kvp.key >> targetIdxItemPairsById.ContainsKey >> not)
      |> Seq.map create
      |> Seq.iter target.Add
      
      // Reorder according to new model list
      for Kvp (sId, (sIdx, _)) in sourceIdxItemPairsById do
        let tIdx =
          target
          |> Seq.indexed
          |> Seq.find (fun (_, t) -> sId = getTargetId t)
          |> fst
        if tIdx <> sIdx then target.Move(tIdx, sIdx)

  /// Updates the binding value (for relevant bindings) and returns a value
  /// indicating whether to trigger PropertyChanged for this binding
  let rec updateValue bindingName newModel = function
    | OneWay { Get = get }
    | TwoWay { Get = get }
    | TwoWayValidate { Get = get } ->
        get currentModel <> get newModel
    | OneWayLazy b ->
        not <| b.Equals (b.Get newModel) (b.Get currentModel)
    | OneWaySeq b ->
        let intermediate = b.Get newModel
        if not <| b.Equals intermediate (b.Get currentModel) then
          let create v _ = v
          let update oldVal newVal oldIdx =
            if not (b.ItemEquals newVal oldVal) then
              b.Values.[oldIdx] <- newVal
          let newVals = intermediate |> b.Map |> Seq.toArray
          oneWaySeqMerge logInvalidGetId logInvalidGetId b.GetId b.GetId create update b.Values newVals
        false
    | Cmd _
    | CmdParam _ ->
        false
    | SubModel b ->
      match !b.Vm, b.GetModel newModel with
      | ValueNone, ValueNone -> false
      | ValueSome _, ValueNone ->
          if b.Sticky then false
          else
            b.Vm := ValueNone
            true
      | ValueNone, ValueSome m ->
          b.Vm := ValueSome <| ViewModel(m, b.ToMsg >> dispatch, b.GetBindings (), config, getPropChainFor bindingName)
          true
      | ValueSome vm, ValueSome m ->
          vm.UpdateModel m
          false
    | SubModelWin b ->
        let winPropChain = getPropChainFor bindingName

        let close () =
          b.PreventClose := false
          match b.WinRef.TryGetTarget () with
          | false, _ ->
              log "[%s] Attempted to close window, but did not find window reference" winPropChain
          | true, w ->
              log "[%s] Closing window" winPropChain
              b.WinRef.SetTarget null
              w.Dispatcher.Invoke(fun () -> w.Close ())
          b.WinRef.SetTarget null

        let hide () =
          match b.WinRef.TryGetTarget () with
          | false, _ ->
              log "[%s] Attempted to hide window, but did not find window reference" winPropChain
          | true, w ->
              log "[%s] Hiding window" winPropChain
              w.Dispatcher.Invoke(fun () -> w.Visibility <- Visibility.Hidden)

        let showHidden () =
          match b.WinRef.TryGetTarget () with
          | false, _ ->
              log "[%s] Attempted to show existing hidden window, but did not find window reference" winPropChain
          | true, w ->
              log "[%s] Showing existing hidden window" winPropChain
              w.Dispatcher.Invoke(fun () -> w.Visibility <- Visibility.Visible)

        let showNew vm initialVisibility =
          b.PreventClose := true
          showNewWindow
            b.WinRef b.GetWindow vm b.IsModal b.OnCloseRequested
            b.PreventClose initialVisibility

        let newVm model =
          ViewModel(model, b.ToMsg >> dispatch, b.GetBindings (), config, getPropChainFor bindingName)

        match !b.VmWinState, b.GetState newModel with
        | WindowState.Closed, WindowState.Closed ->
            false
        | WindowState.Hidden _, WindowState.Closed
        | WindowState.Visible _, WindowState.Closed ->
            close ()
            b.VmWinState := WindowState.Closed
            true
        | WindowState.Closed, WindowState.Hidden m ->
            let vm = newVm m
            log "[%s] Creating hidden window" winPropChain
            showNew vm Visibility.Hidden
            b.VmWinState := WindowState.Hidden vm
            true
        | WindowState.Hidden vm, WindowState.Hidden m ->
            vm.UpdateModel m
            false
        | WindowState.Visible vm, WindowState.Hidden m ->
            hide ()
            vm.UpdateModel m
            b.VmWinState := WindowState.Hidden vm
            false
        | WindowState.Closed, WindowState.Visible m ->
            let vm = newVm m
            log "[%s] Creating and opening window" winPropChain
            showNew vm Visibility.Visible
            b.VmWinState := WindowState.Visible vm
            true
        | WindowState.Hidden vm, WindowState.Visible m ->
            vm.UpdateModel m
            showHidden ()
            b.VmWinState := WindowState.Visible vm
            false
        | WindowState.Visible vm, WindowState.Visible m ->
            vm.UpdateModel m
            false
    | SubModelSeq b ->
        let logInvalidGetTargetId a b (vm: ViewModel<_, _>) = logInvalidGetId a b vm.CurrentModel
        let getTargetId (vm: ViewModel<_, _>) = b.GetId vm.CurrentModel
        let create m id = 
          let chain = getPropChainForItem bindingName (id |> string)
          ViewModel(m, (fun msg -> b.ToMsg (id, msg) |> dispatch), b.GetBindings (), config, chain)
        let update (vm: ViewModel<_, _>) m _ = vm.UpdateModel m
        let newSubModels = newModel |> b.GetModels |> Seq.toArray
        subModelSeqMerge logInvalidGetId logInvalidGetTargetId b.GetId getTargetId create update b.Vms newSubModels
        false
    | SubModelSelectedItem b ->
        b.Get newModel <> b.Get currentModel
    | Cached b ->
        let valueChanged = updateValue bindingName newModel b.Binding
        if valueChanged then
          b.Cache := None
        valueChanged

  /// Returns the command associated with a command binding if the command's
  /// CanExecuteChanged should be triggered.
  let rec getCmdIfCanExecChanged newModel = function
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
    | Cmd { Cmd = cmd; CanExec = canExec } ->
        if canExec newModel = canExec currentModel
        then None
        else Some cmd
    | CmdParam cmd ->
        Some cmd
    | Cached b -> getCmdIfCanExecChanged newModel b.Binding

  let rec tryGetMember model = function
    | OneWay { Get = get }
    | TwoWay { Get = get }
    | TwoWayValidate { Get = get } ->
        get model
    | OneWayLazy b ->
        model |> b.Get |> b.Map
    | OneWaySeq { Values = vals } ->
        box vals
    | Cmd { Cmd = cmd }
    | CmdParam cmd ->
        box cmd
    | SubModel { Vm = vm } -> !vm |> ValueOption.toObj |> box
    | SubModelWin { VmWinState = vm } ->
        match !vm with
        | WindowState.Closed -> null
        | WindowState.Hidden vm | WindowState.Visible vm -> box vm
    | SubModelSeq { Vms = vms } -> box vms
    | SubModelSelectedItem b ->
        let selectedId = b.Get model
        let selected =
          b.SubModelSeqBinding.Vms 
          |> Seq.tryFind (fun (vm: ViewModel<obj, obj>) ->
            selectedId = ValueSome (b.SubModelSeqBinding.GetId vm.CurrentModel))
        log "[%s] Setting selected VM to %A"
          propNameChain
          (selected |> Option.map (fun vm -> b.SubModelSeqBinding.GetId vm.CurrentModel))
        selected |> Option.toObj |> box
    | Cached b ->
        match !b.Cache with
        | Some v -> v
        | None ->
            let v = tryGetMember model b.Binding
            b.Cache := Some v
            v

  let rec trySetMember model (value: obj) = function
    | TwoWay { Set = set }
    | TwoWayValidate { Set = set } ->
        set value model
        true
    | SubModelSelectedItem b ->
        let id =
          (value :?> ViewModel<obj, obj>)
          |> ValueOption.ofObj
          |> ValueOption.map (fun vm -> b.SubModelSeqBinding.GetId vm.CurrentModel)
        b.Set id model
        true
    | Cached b ->
        let successful = trySetMember model value b.Binding
        if successful then
          b.Cache := None  // TODO #185: write test
        successful
    | OneWay _
    | OneWayLazy _
    | OneWaySeq _
    | Cmd _
    | CmdParam _
    | SubModel _
    | SubModelWin _
    | SubModelSeq _ ->
        false

  member __.CurrentModel : 'model = currentModel

  member __.UpdateModel (newModel: 'model) : unit =
    let propsToNotify =
      bindings
      |> Seq.filter (fun (Kvp (name, binding)) -> updateValue name newModel binding)
      |> Seq.map Kvp.key
      |> Seq.toList
    let cmdsToNotify =
      bindings
      |> Seq.choose (Kvp.value >> getCmdIfCanExecChanged newModel)
      |> Seq.toList
    currentModel <- newModel
    propsToNotify |> List.iter notifyPropertyChanged
    cmdsToNotify |> List.iter raiseCanExecuteChanged
    for Kvp (name, binding) in bindings do
      updateValidationError currentModel name binding

  override __.TryGetMember (binder, result) =
    log "[%s] TryGetMember %s" propNameChain binder.Name
    match bindings.TryGetValue binder.Name with
    | false, _ ->
        log "[%s] TryGetMember FAILED: Property %s doesn't exist" propNameChain binder.Name
        false
    | true, binding ->
        result <- tryGetMember currentModel binding
        true

  override __.TrySetMember (binder, value) =
    log "[%s] TrySetMember %s" propNameChain binder.Name
    match bindings.TryGetValue binder.Name with
    | false, _ ->
        log "[%s] TrySetMember FAILED: Property %s doesn't exist" propNameChain binder.Name
        false
    | true, binding ->
        let success = trySetMember currentModel value binding
        if not success then
          log "[%s] TrySetMember FAILED: Binding %s is read-only" propNameChain binder.Name
        success


  interface INotifyPropertyChanged with
    [<CLIEvent>]
    member __.PropertyChanged = propertyChanged.Publish

  interface INotifyDataErrorInfo with
    [<CLIEvent>]
    member __.ErrorsChanged = errorsChanged.Publish
    member __.HasErrors =
      errors.Count > 0
    member __.GetErrors propName =
      log "[%s] GetErrors %s" propNameChain (propName |> Option.ofObj |> Option.defaultValue "<null>")
      match errors.TryGetValue propName with
      | true, err -> upcast [err]
      | false, _ -> upcast []
