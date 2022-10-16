module internal Elmish.WPF.BindingVmHelpers

open System
open System.Windows
open Microsoft.Extensions.Logging

open Elmish


type UpdateData =
  | ErrorsChanged of string
  | PropertyChanged of string
  | CanExecuteChanged of Command

module UpdateData =
  let isPropertyChanged = function PropertyChanged _ -> true | _ -> false


type GetErrorSubModelSelectedItem =
  { NameChain: string
    SubModelSeqBindingName: string
    Id: string }

[<RequireQualifiedAccess>]
type GetError =
  | OneWayToSource
  | SubModelSelectedItem of GetErrorSubModelSelectedItem
  | ToNullError of ValueOption.ToNullError


module Helpers2 =
  let showNewWindow
      (winRef: WeakReference<Window>)
      (getWindow: 'model -> Dispatch<'msg> -> Window)
      (isDialog: bool)
      (onCloseRequested: 'model -> 'msg voption)
      (preventClose: bool ref)
      dataContext
      (initialVisibility: Visibility)
      (getCurrentModel: unit -> 'model)
      (dispatch: 'msg -> unit) =
    let win = getWindow (getCurrentModel ()) dispatch
    winRef.SetTarget win
    (*
     * A different thread might own this Window, so must use its Dispatcher.
     * Invoking asynchronously since ShowDialog is a blocking call. Otherwise,
     * invoking ShowDialog synchronously blocks the Elmish dispatch loop.
     *)
    win.Dispatcher.InvokeAsync(fun () ->
      win.DataContext <- dataContext
      win.Closing.Add(fun ev ->
        ev.Cancel <- preventClose.Value
        getCurrentModel () |> onCloseRequested |> ValueOption.iter dispatch
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

  let measure (logPerformance: ILogger) (performanceLogThresholdMs: int) (name: string) (nameChain: string) (callName: string) f =
    if not <| logPerformance.IsEnabled(LogLevel.Trace) then f
    else
      fun a ->
        let sw = System.Diagnostics.Stopwatch.StartNew ()
        let b = f a
        sw.Stop ()
        if sw.ElapsedMilliseconds >= int64 performanceLogThresholdMs then
          logPerformance.LogTrace("[{BindingNameChain}] {CallName} ({Elapsed}ms): {MeasureName}", nameChain, callName, sw.ElapsedMilliseconds, name)
        b

  let measure2 (logPerformance: ILogger) performanceLogThresholdMs name nameChain callName f =
    if not <| logPerformance.IsEnabled(LogLevel.Trace)
    then f
    else fun a -> measure logPerformance performanceLogThresholdMs name nameChain callName (f a)


type OneWayBinding<'model, 'a> = {
  OneWayData: OneWayData<'model, 'a>
}

type OneWayToSourceBinding<'model, 'a> = {
  Set: 'a -> 'model -> unit
}

type OneWaySeqBinding<'model, 'a, 'aCollection, 'id when 'id : equality> = {
  OneWaySeqData: OneWaySeqData<'model, 'a, 'aCollection, 'id>
  Values: CollectionTarget<'a, 'aCollection>
}

type TwoWayBinding<'model, 'a> = {
  Get: 'model -> 'a
  Set: 'a -> 'model -> unit
}

type SubModelBinding<'model, 'msg, 'bindingModel, 'bindingMsg, 'vm> = {
  SubModelData: SubModelData<'model, 'msg, 'bindingModel, 'bindingMsg, 'vm>
  Dispatch: 'msg -> unit
  GetVm: unit -> 'vm voption
  SetVm: 'vm voption -> unit
  GetCurrentModel: unit -> 'model
}

type SubModelWinBinding<'model, 'msg, 'bindingModel, 'bindingMsg, 'vm> = {
  SubModelWinData: SubModelWinData<'model, 'msg, 'bindingModel, 'bindingMsg, 'vm>
  Dispatch: 'msg -> unit
  WinRef: WeakReference<Window>
  PreventClose: bool ref
  GetVmWinState: unit -> WindowState<'vm>
  SetVmWinState: WindowState<'vm> -> unit
  GetCurrentModel: unit -> 'model
}

type SubModelSeqUnkeyedBinding<'model, 'msg, 'bindingModel, 'bindingMsg, 'vm, 'vmCollection> = {
  SubModelSeqUnkeyedData: SubModelSeqUnkeyedData<'model, 'msg, 'bindingModel, 'bindingMsg, 'vm, 'vmCollection>
  Dispatch: 'msg -> unit
  Vms: CollectionTarget<'vm, 'vmCollection>
  GetCurrentModel: unit -> 'model
}

type SubModelSeqKeyedBinding<'model, 'msg, 'bindingModel, 'bindingMsg, 'vm, 'vmCollection, 'id when 'id : equality> =
  { SubModelSeqKeyedData: SubModelSeqKeyedData<'model, 'msg, 'bindingModel, 'bindingMsg, 'vm, 'vmCollection, 'id>
    Dispatch: 'msg -> unit
    Vms: CollectionTarget<'vm, 'vmCollection>
    GetCurrentModel: unit -> 'model
  }

  member b.FromId(id: 'id) =
    b.Vms.Enumerate ()
    |> Seq.tryFind (fun vm -> vm |> b.SubModelSeqKeyedData.VmToId |> (=) id)

type SelectedItemBinding<'bindingModel, 'bindingMsg, 'vm, 'id> = {
  FromId: 'id -> 'vm option
  VmToId: 'vm -> 'id
}

type SubModelSelectedItemBinding<'model, 'msg, 'bindingModel, 'bindingMsg, 'vm, 'id> =
  { Get: 'model -> 'id voption
    Set: 'id voption -> 'model -> unit
    SubModelSeqBindingName: string
    SelectedItemBinding: SelectedItemBinding<'bindingModel, 'bindingMsg, 'vm, 'id>
  }

  member b.TypedGet(model: 'model) =
    b.Get model |> ValueOption.map (fun selectedId -> selectedId, b.SelectedItemBinding.FromId selectedId)

  member b.TypedSet(model: 'model, vm: 'vm voption) =
    let id = vm |> ValueOption.map b.SelectedItemBinding.VmToId
    b.Set id model


type BaseVmBinding<'model, 'msg, 't> =
  | OneWay of OneWayBinding<'model, 't>
  | OneWayToSource of OneWayToSourceBinding<'model, 't>
  | OneWaySeq of OneWaySeqBinding<'model, obj, 't, obj>
  | TwoWay of TwoWayBinding<'model, 't>
  | Cmd of cmd: Command
  | SubModel of SubModelBinding<'model, 'msg, obj, obj, 't>
  | SubModelWin of SubModelWinBinding<'model, 'msg, obj, obj, 't>
  | SubModelSeqUnkeyed of SubModelSeqUnkeyedBinding<'model, 'msg, obj, obj, obj, 't>
  | SubModelSeqKeyed of SubModelSeqKeyedBinding<'model, 'msg, obj, obj, obj, 't, obj>
  | SubModelSelectedItem of SubModelSelectedItemBinding<'model, 'msg, obj, obj, 't, obj>


type CachedBinding<'model, 'msg, 't> = {
  Binding: VmBinding<'model, 'msg, 't>
  GetCache: unit -> 't option
  SetCache: 't option -> unit
}

and ValidationBinding<'model, 'msg, 't> = {
  Binding: VmBinding<'model, 'msg, 't>
  Validate: 'model -> string list
  Errors: string list ref
}

and LazyBinding<'model, 'msg, 'bindingModel, 'bindingMsg, 't> = {
  Binding: VmBinding<'bindingModel, 'bindingMsg, 't>
  Get: 'model -> 'bindingModel
  Equals: 'bindingModel -> 'bindingModel -> bool
}

and AlterMsgStreamBinding<'model, 'bindingModel, 'bindingMsg, 't> = {
  Binding: VmBinding<'bindingModel, 'bindingMsg, 't>
  Get: 'model -> 'bindingModel
}

/// Represents all necessary data used in an active binding.
and VmBinding<'model, 'msg, 't> =
  | BaseVmBinding of BaseVmBinding<'model, 'msg, 't>
  | Cached of CachedBinding<'model, 'msg, 't>
  | Validatation of ValidationBinding<'model, 'msg, 't>
  | Lazy of LazyBinding<'model, 'msg, obj, obj, 't>
  | AlterMsgStream of AlterMsgStreamBinding<'model, obj, obj, 't>

  with

    member this.AddCaching = let mutable cache = None in Cached { Binding = this; GetCache = (fun () -> cache); SetCache = fun c -> cache <- c }
    member this.AddValidation currentModel validate =
      { Binding = this
        Validate = validate
        Errors = currentModel |> validate |> ref }
      |> Validatation


type SubModelSelectedItemLast() =

  member _.Base(data: BaseBindingData<'model, 'msg, obj>) : int =
    match data with
    | SubModelSelectedItemData _ -> 1
    | _ -> 0

  member this.Recursive<'model, 'msg>(data: BindingData<'model, 'msg, obj>) : int =
    match data with
    | BaseBindingData d -> this.Base d
    | CachingData d -> this.Recursive d
    | ValidationData d -> this.Recursive d.BindingData
    | LazyData d -> this.Recursive d.BindingData
    | AlterMsgStreamData d -> this.Recursive d.BindingData

  member this.CompareBindingDatas() : BindingData<'model, 'msg, obj> -> BindingData<'model, 'msg, obj> -> int =
    fun a b -> this.Recursive(a) - this.Recursive(b)


type FirstValidationErrors() =

  member this.Recursive<'model, 'msg, 't>
      (binding: VmBinding<'model, 'msg, 't>)
      : string list ref option =
    match binding with
    | BaseVmBinding _ -> None
    | Cached b -> this.Recursive b.Binding
    | Lazy b -> this.Recursive b.Binding
    | AlterMsgStream b -> this.Recursive b.Binding
    | Validatation b -> b.Errors |> Some // TODO: what if there is more than one validation effect?


type FuncsFromSubModelSeqKeyed() =

  member _.Base(binding: BaseVmBinding<'model, 'msg, 't>) : SelectedItemBinding<'a, 'b, 'c, obj> option =
    match binding with
    | SubModelSeqKeyed b ->
      { VmToId = box >> b.SubModelSeqKeyedData.VmToId
        FromId = b.FromId >> Option.map unbox }
      |> Some
    | _ -> None

  member this.Recursive<'model, 'msg, 't>
      (binding: VmBinding<'model, 'msg, 't>)
      : SelectedItemBinding<obj, obj, 't, obj> option =
    match binding with
    | BaseVmBinding b -> this.Base b
    | Cached b -> this.Recursive b.Binding
    | Validatation b -> this.Recursive b.Binding
    | Lazy b -> this.Recursive b.Binding
    | AlterMsgStream b -> this.Recursive b.Binding


type Initialize<'t>
      (loggingArgs: LoggingViewModelArgs,
       name: string,
       getFunctionsForSubModelSelectedItem: string -> SelectedItemBinding<obj, obj, 't, obj> option) =

  let { log = log
        logPerformance = logPerformance
        performanceLogThresholdMs = performanceLogThresholdMs
        nameChain = nameChain } =
    loggingArgs

  let measure x = x |> Helpers2.measure logPerformance performanceLogThresholdMs name nameChain
  let measure2 x = x |> Helpers2.measure2 logPerformance performanceLogThresholdMs name nameChain

  member _.Base<'model, 'msg>
      (initialModel: 'model,
       dispatch: 'msg -> unit,
       getCurrentModel: unit -> 'model,
       binding: BaseBindingData<'model, 'msg, 't>)
      : BaseVmBinding<'model, 'msg, 't> option =
    match binding with
      | OneWayData d ->
          { OneWayData = d |> BindingData.OneWay.measureFunctions measure }
          |> OneWay
          |> Some
      | OneWayToSourceData d ->
          let d = d |> BindingData.OneWayToSource.measureFunctions measure
          { Set = fun obj m -> d.Set obj m |> dispatch }
          |> OneWayToSource
          |> Some
      | OneWaySeqData d ->
          { OneWaySeqData = d |> BindingData.OneWaySeq.measureFunctions measure measure measure2
            Values = d.CreateCollection (initialModel |> d.Get) }
          |> OneWaySeq
          |> Some
      | TwoWayData d ->
          let d = d |> BindingData.TwoWay.measureFunctions measure measure
          { Get = d.Get
            Set = fun obj m -> d.Set obj m |> dispatch }
          |> TwoWay
          |> Some
      | CmdData d ->
          let d = d |> BindingData.Cmd.measureFunctions measure2 measure2
          let execute param = d.Exec param (getCurrentModel ()) |> ValueOption.iter dispatch
          let canExecute param = d.CanExec param (getCurrentModel ())
          let cmd = Command(execute, canExecute)
          if d.AutoRequery then
            cmd.AddRequeryHandler ()
          cmd
          |> Cmd
          |> Some
      | SubModelData d ->
          let d = d |> BindingData.SubModel.measureFunctions measure measure measure2
          let toMsg = fun msg -> d.ToMsg (getCurrentModel ()) msg
          let chain = LoggingViewModelArgs.getNameChainFor nameChain name
          d.GetModel initialModel
          |> ValueOption.map (fun m -> ViewModelArgs.create m (toMsg >> dispatch) chain loggingArgs)
          |> ValueOption.map d.CreateViewModel
          |> (fun vm -> let mutable vm = vm in { SubModelData = d
                                                 Dispatch = dispatch
                                                 GetVm = (fun () -> vm)
                                                 SetVm = fun nvm -> vm <- nvm
                                                 GetCurrentModel = getCurrentModel
                                               })
          |> SubModel
          |> Some
      | SubModelWinData d ->
          let d = d |> BindingData.SubModelWin.measureFunctions measure measure measure2
          let toMsg = fun msg -> d.ToMsg (getCurrentModel ()) msg
          match d.GetState initialModel with
          | WindowState.Closed ->
              let mutable vmWinState = WindowState.Closed
              { SubModelWinData = d
                Dispatch = dispatch
                WinRef = WeakReference<_>(null)
                PreventClose = ref true
                GetVmWinState = fun () -> vmWinState
                SetVmWinState = fun vmState -> vmWinState <- vmState
                GetCurrentModel = getCurrentModel
              }
          | WindowState.Hidden m ->
              let chain = LoggingViewModelArgs.getNameChainFor nameChain name
              let args = ViewModelArgs.create m (toMsg >> dispatch) chain loggingArgs
              let vm = d.CreateViewModel args
              let winRef = WeakReference<_>(null)
              let preventClose = ref true
              log.LogTrace("[{BindingNameChain}] Creating hidden window", chain)
              Helpers2.showNewWindow winRef d.GetWindow d.IsModal d.OnCloseRequested preventClose vm Visibility.Hidden getCurrentModel dispatch
              let mutable vmWinState = WindowState.Hidden vm
              { SubModelWinData = d
                Dispatch = dispatch
                WinRef = winRef
                PreventClose = preventClose
                GetVmWinState = fun () -> vmWinState
                SetVmWinState = fun vm -> vmWinState <- vm
                GetCurrentModel = getCurrentModel
              }
          | WindowState.Visible m ->
              let chain = LoggingViewModelArgs.getNameChainFor nameChain name
              let args = ViewModelArgs.create m (toMsg >> dispatch) chain loggingArgs
              let vm = d.CreateViewModel args
              let winRef = WeakReference<_>(null)
              let preventClose = ref true
              log.LogTrace("[{BindingNameChain}] Creating visible window", chain)
              Helpers2.showNewWindow winRef d.GetWindow d.IsModal d.OnCloseRequested preventClose vm Visibility.Visible getCurrentModel dispatch
              let mutable vmWinState = WindowState.Visible vm
              { SubModelWinData = d
                Dispatch = dispatch
                WinRef = winRef
                PreventClose = preventClose
                GetVmWinState = fun () -> vmWinState
                SetVmWinState = fun vm -> vmWinState <- vm
                GetCurrentModel = getCurrentModel
              }
          |> SubModelWin
          |> Some
      | SubModelSeqUnkeyedData d ->
          let d = d |> BindingData.SubModelSeqUnkeyed.measureFunctions measure measure measure2
          let toMsg = fun msg -> d.ToMsg (getCurrentModel ()) msg
          let vms =
            d.GetModels initialModel
            |> Seq.indexed
            |> Seq.map (fun (idx, m) ->
                 let chain = LoggingViewModelArgs.getNameChainForItem nameChain name (idx |> string)
                 let args = ViewModelArgs.create m (fun msg -> toMsg (idx, msg) |> dispatch) chain loggingArgs
                 d.CreateViewModel args)
            |> d.CreateCollection
          { SubModelSeqUnkeyedData = d
            Dispatch = dispatch
            Vms = vms
            GetCurrentModel = getCurrentModel
          }
          |> SubModelSeqUnkeyed
          |> Some
      | SubModelSeqKeyedData d ->
          let d = d |> BindingData.SubModelSeqKeyed.measureFunctions measure measure measure2 measure
          let toMsg = fun msg -> d.ToMsg (getCurrentModel ()) msg
          let vms =
            d.GetSubModels initialModel
            |> Seq.map (fun m ->
                 let mId = d.BmToId m
                 let chain = LoggingViewModelArgs.getNameChainForItem nameChain name (mId |> string)
                 let args = ViewModelArgs.create m (fun msg -> toMsg (mId, msg) |> dispatch) chain loggingArgs
                 d.CreateViewModel args)
            |> d.CreateCollection
          { SubModelSeqKeyedData = d
            Dispatch = dispatch
            Vms = vms
            GetCurrentModel = getCurrentModel
          }
          |> SubModelSeqKeyed
          |> Some
      | SubModelSelectedItemData d ->
          let d = d |> BindingData.SubModelSelectedItem.measureFunctions measure measure2
          d.SubModelSeqBindingName
          |> getFunctionsForSubModelSelectedItem
          |> Option.map (fun selectedItemBinding ->
              { Get = d.Get
                Set = fun obj m -> d.Set obj m |> dispatch
                SubModelSeqBindingName = d.SubModelSeqBindingName
                SelectedItemBinding = selectedItemBinding }
              |> SubModelSelectedItem)

  member this.Recursive<'model, 'msg>
      (initialModel: 'model,
       dispatch: 'msg -> unit,
       getCurrentModel: unit -> 'model,
       binding: BindingData<'model, 'msg, 't>)
      : VmBinding<'model, 'msg, 't> option =
    option {
      match binding with
      | BaseBindingData d ->
          let! b = this.Base(initialModel, dispatch, getCurrentModel, d)
          return BaseVmBinding b
      | CachingData d ->
          let! b = this.Recursive(initialModel, dispatch, getCurrentModel, d)
          return b.AddCaching
      | ValidationData d ->
          let d = d |> BindingData.Validation.measureFunctions measure
          let! b = this.Recursive(initialModel, dispatch, getCurrentModel, d.BindingData)
          return b.AddValidation initialModel d.Validate
      | LazyData d ->
          let initialModel' : obj = d.Get initialModel
          let getCurrentModel' : unit -> obj = getCurrentModel >> d.Get
          let dispatch' : obj -> unit = d.MapDispatch(getCurrentModel, dispatch)
          let d = d |> BindingData.Lazy.measureFunctions measure measure2 measure2
          let! b = this.Recursive(initialModel', dispatch', getCurrentModel', d.BindingData)
          return { Binding = b
                   Get = d.Get
                   Equals = d.Equals
                 } |> Lazy
      | AlterMsgStreamData d ->
          let initialModel' : obj = d.Get initialModel
          let getCurrentModel' : unit -> obj = getCurrentModel >> d.Get
          let dispatch' : obj -> unit = d.MapDispatch(getCurrentModel, dispatch)
          let! b = this.Recursive(initialModel', dispatch', getCurrentModel', d.BindingData)
          return { Binding = b
                   Get = d.Get
                 } |> AlterMsgStream
    }


/// Updates the binding and returns a list indicating what events to raise for this binding
type Update<'t>
    (loggingArgs: LoggingViewModelArgs,
     name: string) =

  let { log = log
        nameChain = nameChain } =
    loggingArgs

  member _.Base<'model, 'msg>
      (newModel: 'model,
       binding: BaseVmBinding<'model, 'msg, 't>) =
    match binding with
      | OneWay _
      | TwoWay _
      | SubModelSelectedItem _ -> [ PropertyChanged name ]
      | OneWayToSource _ -> []
      | OneWaySeq b ->
          b.OneWaySeqData.Merge(b.Values, newModel)
          []
      | Cmd cmd -> cmd |> CanExecuteChanged |> List.singleton
      | SubModel b ->
        let d = b.SubModelData
        match b.GetVm (), d.GetModel newModel with
        | ValueNone, ValueNone -> []
        | ValueSome _, ValueNone ->
            b.SetVm ValueNone
            [ PropertyChanged name ]
        | ValueNone, ValueSome m ->
            let toMsg = fun msg -> d.ToMsg (b.GetCurrentModel ()) msg
            let chain = LoggingViewModelArgs.getNameChainFor nameChain name
            let args = ViewModelArgs.create m (toMsg >> b.Dispatch) chain loggingArgs
            b.SetVm (ValueSome <| d.CreateViewModel(args))
            [ PropertyChanged name ]
        | ValueSome vm, ValueSome m ->
            d.UpdateViewModel (vm, m)
            []
      | SubModelWin b ->
          let d = b.SubModelWinData
          let winPropChain = LoggingViewModelArgs.getNameChainFor nameChain name
          let close () =
            b.PreventClose.Value <- false
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
            b.PreventClose.Value <- true
            Helpers2.showNewWindow b.WinRef d.GetWindow d.IsModal d.OnCloseRequested b.PreventClose vm

          let newVm model =
            let toMsg = fun msg -> d.ToMsg (b.GetCurrentModel ()) msg
            let chain = LoggingViewModelArgs.getNameChainFor nameChain name
            let args = ViewModelArgs.create model (toMsg >> b.Dispatch) chain loggingArgs
            d.CreateViewModel args

          match b.GetVmWinState(), d.GetState newModel with
          | WindowState.Closed, WindowState.Closed ->
              []
          | WindowState.Hidden vm, WindowState.Hidden m
          | WindowState.Visible vm, WindowState.Visible m ->
              d.UpdateViewModel (vm, m)
              []
          | WindowState.Hidden _, WindowState.Closed
          | WindowState.Visible _, WindowState.Closed ->
              close ()
              b.SetVmWinState WindowState.Closed
              [ PropertyChanged name ]
          | WindowState.Visible vm, WindowState.Hidden m ->
              hide ()
              d.UpdateViewModel (vm, m)
              b.SetVmWinState (WindowState.Hidden vm)
              []
          | WindowState.Hidden vm, WindowState.Visible m ->
              d.UpdateViewModel (vm, m)
              showHidden ()
              b.SetVmWinState (WindowState.Visible vm)
              []
          | WindowState.Closed, WindowState.Hidden m ->
              let vm = newVm m
              log.LogTrace("[{BindingNameChain}] Creating hidden window", winPropChain)
              showNew vm Visibility.Hidden b.GetCurrentModel b.Dispatch
              b.SetVmWinState (WindowState.Hidden vm)
              [ PropertyChanged name ]
          | WindowState.Closed, WindowState.Visible m ->
              let vm = newVm m
              log.LogTrace("[{BindingNameChain}] Creating visible window", winPropChain)
              showNew vm Visibility.Visible b.GetCurrentModel b.Dispatch
              b.SetVmWinState (WindowState.Visible vm)
              [ PropertyChanged name ]
      | SubModelSeqUnkeyed b ->
          let d = b.SubModelSeqUnkeyedData
          let create m idx =
            let chain = LoggingViewModelArgs.getNameChainForItem nameChain name (idx |> string)
            let args = ViewModelArgs.create m (fun msg -> d.ToMsg (b.GetCurrentModel ()) (idx, msg) |> b.Dispatch) chain loggingArgs
            d.CreateViewModel args
          let update vm m = d.UpdateViewModel (vm, m)
          Merge.unkeyed create update b.Vms (d.GetModels newModel)
          []
      | SubModelSeqKeyed b ->
          let d = b.SubModelSeqKeyedData
          let create m id =
            let chain = LoggingViewModelArgs.getNameChainForItem nameChain name (id |> string)
            let args = ViewModelArgs.create m (fun msg -> d.ToMsg (b.GetCurrentModel ()) (id, msg) |> b.Dispatch) chain loggingArgs
            d.CreateViewModel args
          let update vm m = d.UpdateViewModel (vm, m)
          let newSubModels = newModel |> d.GetSubModels |> Seq.toArray
          try
            d.MergeKeyed(create, update, b.Vms, newSubModels)
          with
            | :? DuplicateIdException as e ->
              let messageTemplate = "[{BindingNameChain}] In the {SourceOrTarget} sequence of the binding {BindingName}, the elements at indices {Index1} and {Index2} have the same ID {ID}. To avoid this problem, the elements will be merged without using IDs."
              log.LogError(messageTemplate, nameChain, e.SourceOrTarget, name, e.Index1, e.Index2, e.Id)
              let create m _ = create m (d.BmToId m)
              Merge.unkeyed create update b.Vms newSubModels
          []

  member this.Recursive<'model, 'msg>
      (currentModel: 'model,
       newModel: 'model,
       binding: VmBinding<'model, 'msg, 't>)
      : UpdateData list =
    match binding with
      | BaseVmBinding b -> this.Base(newModel, b)
      | Cached b ->
          let updates = this.Recursive(currentModel, newModel, b.Binding)
          updates
          |> List.filter UpdateData.isPropertyChanged
          |> List.iter (fun _ -> b.SetCache None)
          updates
      | Validatation b ->
          let updates = this.Recursive(currentModel, newModel, b.Binding)
          let newErrors = b.Validate newModel
          if b.Errors.Value <> newErrors then
            b.Errors.Value <- newErrors
            ErrorsChanged name :: updates
          else
            updates
      | Lazy b ->
          let currentModel' = currentModel |> b.Get
          let newModel' = newModel |> b.Get
          if b.Equals currentModel' newModel' then
            []
          else
            this.Recursive(currentModel', newModel', b.Binding)
      | AlterMsgStream b ->
          this.Recursive(currentModel |> b.Get, b.Get newModel, b.Binding)


type Get<'t>(nameChain: string) =

  member _.Base (model: 'model, binding: BaseVmBinding<'model, 'msg, 't>) =
    match binding with
    | OneWay { OneWayData = d } -> d.Get model |> Ok
    | TwoWay b -> b.Get model |> Ok
    | OneWayToSource _ -> GetError.OneWayToSource |> Error
    | OneWaySeq { Values = vals } -> vals.GetCollection () |> Ok
    | Cmd cmd -> cmd |> unbox |> Ok
    | SubModel { GetVm = getvm } -> getvm() |> ValueOption.toNull |> Result.mapError GetError.ToNullError
    | SubModelWin { GetVmWinState = getvm } ->
        getvm()
        |> WindowState.toVOption
        |> ValueOption.toNull
        |> Result.mapError GetError.ToNullError
    | SubModelSeqUnkeyed { Vms = vms }
    | SubModelSeqKeyed { Vms = vms } -> vms.GetCollection () |> Ok
    | SubModelSelectedItem b ->
        b.TypedGet model
        |> function
          | ValueNone -> ValueNone |> Ok // deselecting successful
          | ValueSome (id, mVm) ->
              match mVm with
              | Some vm -> vm |> ValueSome |> Ok // selecting successful
              | None -> // selecting failed
                  { NameChain = nameChain
                    SubModelSeqBindingName = b.SubModelSeqBindingName
                    Id = id.ToString() }
                  |> GetError.SubModelSelectedItem
                  |> Error
        |> Result.bind (ValueOption.toNull >> Result.mapError GetError.ToNullError)

  member this.Recursive<'model, 'msg>
      (model: 'model,
       binding: VmBinding<'model, 'msg, 't>)
      : Result<'t, GetError> =
    match binding with
    | BaseVmBinding b -> this.Base(model, b)
    | Cached b ->
        match b.GetCache() with
        | Some v -> v |> Ok
        | None ->
            let x = this.Recursive(model, b.Binding)
            x |> Result.iter (fun v -> b.SetCache (Some v))
            x
    | Validatation b -> this.Recursive(model, b.Binding)
    | Lazy b -> this.Recursive(b.Get model, b.Binding)
    | AlterMsgStream b -> this.Recursive(b.Get model, b.Binding)


type Set<'t>(value: 't) =

  member _.Base(model: 'model, binding: BaseVmBinding<'model, 'msg, 't>) =
    match binding with
    | TwoWay b ->
        b.Set value model
        true
    | OneWayToSource b ->
        b.Set value model
        true
    | SubModelSelectedItem b ->
        b.TypedSet(model, ValueOption.ofNull value)
        true
    | OneWay _
    | OneWaySeq _
    | Cmd _
    | SubModel _
    | SubModelWin _
    | SubModelSeqUnkeyed _
    | SubModelSeqKeyed _ ->
        false

  member this.Recursive<'model, 'msg>(model: 'model, binding: VmBinding<'model, 'msg, 't>) : bool =
    match binding with
    | BaseVmBinding b -> this.Base(model, b)
    | Cached b ->
        // UpdateModel changes the model,
        // but Set only dispatches a message,
        // so don't clear the cache here
        this.Recursive<'model, 'msg>(model, b.Binding)
    | Validatation b -> this.Recursive<'model, 'msg>(model, b.Binding)
    | Lazy b -> this.Recursive<obj, obj>(b.Get model, b.Binding)
    | AlterMsgStream b -> this.Recursive<obj, obj>(b.Get model, b.Binding)
