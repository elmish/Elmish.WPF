namespace Elmish.WPF

open System
open System.Dynamic
open System.Collections.Generic
open System.Collections.ObjectModel
open System.ComponentModel
open System.Windows
open Microsoft.Extensions.Logging
open System.Linq.Expressions

open Elmish
open System.Runtime.CompilerServices
open Microsoft.Extensions.Logging.Abstractions
open System.Windows.Input


type internal UpdateData =
  | ErrorsChanged of string
  | PropertyChanged of string
  | CanExecuteChanged of Command

module internal UpdateData =
  let isPropertyChanged = function PropertyChanged _ -> true | _ -> false


type internal GetErrorSubModelSelectedItem =
  { NameChain: string
    SubModelSeqBindingName: string
    Id: string }

[<RequireQualifiedAccess>]
type internal GetError =
  | OneWayToSource
  | SubModelSelectedItem of GetErrorSubModelSelectedItem


module internal Helpers2 =
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
    if not <| logPerformance.IsEnabled(LogLevel.Trace) then f
    else fun a -> measure logPerformance performanceLogThresholdMs name nameChain callName (f a)


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
  Vm: IViewModel<'bindingModel> voption ref
}

and internal SubModelVmBinding<'model, 'msg, 'bindingModel, 'bindingMsg, 'viewModel when 'viewModel :> IViewModel> = {
  SubModelVmData: SubModelVmData<'model, 'msg, 'bindingModel, 'bindingMsg, 'viewModel>
  Vm: 'viewModel voption ref
}

and internal SubModelWinBinding<'model, 'msg, 'bindingModel, 'bindingMsg> = {
  SubModelWinData: SubModelWinData<'model, 'msg, 'bindingModel, 'bindingMsg>
  WinRef: WeakReference<Window>
  PreventClose: bool ref
  VmWinState: WindowState<IViewModel<'bindingModel>> ref
}

and internal SubModelSeqUnkeyedBinding<'model, 'msg, 'bindingModel, 'bindingMsg> = {
  SubModelSeqUnkeyedData: SubModelSeqUnkeyedData<'model, 'msg, 'bindingModel, 'bindingMsg>
  Vms: ObservableCollection<IViewModel<'bindingModel>>
}

and internal SubModelSeqKeyedBinding<'model, 'msg, 'bindingModel, 'bindingMsg, 'id when 'id : equality> =
  { SubModelSeqKeyedData: SubModelSeqKeyedData<'model, 'msg, 'bindingModel, 'bindingMsg, 'id>
    Vms: ObservableCollection<IViewModel<'bindingModel>> }

  member d.FromId(id: 'id) =
    d.Vms
    |> Seq.tryFind (fun vm -> vm.CurrentModel |> d.SubModelSeqKeyedData.GetId |> (=) id)

and internal SubModelSelectedItemBinding<'model, 'msg, 'bindingModel, 'bindingMsg, 'id> =
  { Get: 'model -> 'id voption
    Set: 'id voption -> 'model -> unit
    SubModelSeqBindingName: string
    GetId: 'bindingModel -> 'id
    FromId: 'id -> IViewModel<'bindingModel> option }

  member d.TryGetMember (model: 'model) =
    d.Get model |> ValueOption.map (fun selectedId -> selectedId, d.FromId selectedId)

  member d.TrySetMember
      (model: 'model,
       bindingModel: 'bindingModel voption) =
    let id = bindingModel |> ValueOption.map d.GetId
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

and internal AlterMsgStreamBinding<'model, 'bindingModel, 'bindingMsg> = {
  Binding: VmBinding<'bindingModel, 'bindingMsg>
  Get: 'model -> 'bindingModel
  Dispatch: 'bindingMsg -> unit
}


and internal BaseVmBinding<'model, 'msg> =
  | OneWay of OneWayBinding<'model>
  | OneWayToSource of OneWayToSourceBinding<'model>
  | OneWaySeq of OneWaySeqBinding<'model, obj, obj, obj>
  | TwoWay of TwoWayBinding<'model>
  | Cmd of cmd: Command
  | SubModelVm of SubModelVmBinding<'model, 'msg, obj, obj, IViewModel>
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
  | AlterMsgStream of AlterMsgStreamBinding<'model, obj, obj>

  with

    member this.AddCaching = Cached { Binding = this; Cache = ref None }
    member this.AddLazy equals = { Binding = this; Equals = equals } |> Lazy
    member this.AddValidation currentModel validate =
      { Binding = this
        Validate = validate
        Errors = currentModel |> validate |> ref }
      |> Validatation


and internal SubModelSelectedItemLast() =

  member _.Base(data: BaseBindingData<'model, 'msg>) : int =
    match data with
    | SubModelSelectedItemData _ -> 1
    | _ -> 0

  member this.Recursive<'model, 'msg>(data: BindingData<'model, 'msg>) : int =
    match data with
    | BaseBindingData d -> this.Base d
    | CachingData d -> this.Recursive d
    | ValidationData d -> this.Recursive d.BindingData
    | LazyData d -> this.Recursive d.BindingData
    | AlterMsgStreamData d -> this.Recursive d.BindingData

  member this.CompareBindingDatas() : BindingData<'model, 'msg> -> BindingData<'model, 'msg> -> int =
    fun a b -> this.Recursive(a) - this.Recursive(b)

  member this.CompareBindings() : Binding<'model, 'msg> -> Binding<'model, 'msg> -> int =
    fun a b -> this.Recursive(a.Data) - this.Recursive(b.Data)



and internal FirstValidationErrors() =

  member this.Recursive<'model, 'msg>
      (binding: VmBinding<'model, 'msg>)
      : string list ref option =
    match binding with
    | BaseVmBinding _ -> None
    | Cached b -> this.Recursive b.Binding
    | Lazy b -> this.Recursive b.Binding
    | AlterMsgStream b -> this.Recursive b.Binding
    | Validatation b -> b.Errors |> Some // TODO: what if there is more than one validation effect?


and internal FuncsFromSubModelSeqKeyed() =

  member _.Base(binding: BaseVmBinding<'model, 'msg>) =
    match binding with
    | SubModelSeqKeyed b -> Some (b.SubModelSeqKeyedData.GetId, b.FromId)
    | _ -> None

  member this.Recursive<'model, 'msg>
      (binding: VmBinding<'model, 'msg>)
      : ((obj -> obj) * (obj -> IViewModel<obj> option)) option =
    match binding with
    | BaseVmBinding b -> this.Base b
    | Cached b -> this.Recursive b.Binding
    | Validatation b -> this.Recursive b.Binding
    | Lazy b -> this.Recursive b.Binding
    | AlterMsgStream b -> this.Recursive b.Binding


and internal Initialize
      (log: ILogger,
       logPerformance: ILogger,
       performanceLogThresholdMs: int,
       nameChain: string,
       getNameChainFor: string -> string,
       getNameChainForItem: string -> string -> string,
       name: string,
       getFunctionsForSubModelSelectedItem: string -> ((obj -> obj) * (obj -> IViewModel<obj> option)) option) =

  let measure x = x |> Helpers2.measure logPerformance performanceLogThresholdMs name nameChain
  let measure2 x = x |> Helpers2.measure2 logPerformance performanceLogThresholdMs name nameChain

  member _.Base<'model, 'msg>
      (initialModel: 'model,
       getCurrentModel: unit -> 'model,
       dispatch: 'msg -> unit,
       binding: BaseBindingData<'model, 'msg>)
      : BaseVmBinding<'model, 'msg> option =
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
      | OneWaySeqLazyData d ->
          { OneWaySeqData = d |> BindingData.OneWaySeqLazy.measureFunctions measure measure measure2 measure measure2
            Values = ObservableCollection(initialModel |> d.Get |> d.Map) }
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
          d.GetModel initialModel
          |> ValueOption.map (fun m -> DynamicViewModel(m, toMsg >> dispatch, d.GetBindings (), performanceLogThresholdMs, getNameChainFor name, log, logPerformance) :> IViewModel<_>)
          |> (fun vm -> { SubModelData = d; Vm = ref vm })
          |> SubModel
          |> Some
      | SubModelVmData d ->
          let d = d |> BindingData.SubModelVm.measureFunctions measure measure measure2
          let toMsg = fun msg -> d.ToMsg (getCurrentModel ()) msg
          d.GetModel initialModel
          |> ValueOption.map (fun m -> d.CreateViewModel(m, toMsg >> dispatch))
          |> (fun vm -> { SubModelVmData = d; Vm = ref vm })
          |> SubModelVm
          |> Some
      | SubModelWinData d ->
          let d = d |> BindingData.SubModelWin.measureFunctions measure measure measure2
          let toMsg = fun msg -> d.ToMsg (getCurrentModel ()) msg
          match d.GetState initialModel with
          | WindowState.Closed ->
              { SubModelWinData = d
                WinRef = WeakReference<_>(null)
                PreventClose = ref true
                VmWinState = ref WindowState.Closed }
          | WindowState.Hidden m ->
              let chain = getNameChainFor name
              let vm = DynamicViewModel(m, toMsg >> dispatch, d.GetBindings (), performanceLogThresholdMs, chain, log, logPerformance)
              let winRef = WeakReference<_>(null)
              let preventClose = ref true
              log.LogTrace("[{BindingNameChain}] Creating hidden window", chain)
              Helpers2.showNewWindow winRef d.GetWindow d.IsModal d.OnCloseRequested preventClose vm Visibility.Hidden getCurrentModel dispatch
              { SubModelWinData = d
                WinRef = winRef
                PreventClose = preventClose
                VmWinState = vm :> IViewModel<_> |> WindowState.Hidden |> ref }
          | WindowState.Visible m ->
              let chain = getNameChainFor name
              let vm = DynamicViewModel(m, toMsg >> dispatch, d.GetBindings (), performanceLogThresholdMs, chain, log, logPerformance)
              let winRef = WeakReference<_>(null)
              let preventClose = ref true
              log.LogTrace("[{BindingNameChain}] Creating visible window", chain)
              Helpers2.showNewWindow winRef d.GetWindow d.IsModal d.OnCloseRequested preventClose vm Visibility.Visible getCurrentModel dispatch
              { SubModelWinData = d
                WinRef = winRef
                PreventClose = preventClose
                VmWinState = vm :> IViewModel<_> |> WindowState.Visible |> ref }
          |> SubModelWin
          |> Some
      | SubModelSeqUnkeyedData d ->
          let d = d |> BindingData.SubModelSeqUnkeyed.measureFunctions measure measure measure2
          let toMsg = fun msg -> d.ToMsg (getCurrentModel ()) msg
          let vms =
            d.GetModels initialModel
            |> Seq.indexed
            |> Seq.map (fun (idx, m) ->
                 let chain = getNameChainForItem name (idx |> string)
                 DynamicViewModel(m, (fun msg -> toMsg (idx, msg) |> dispatch), d.GetBindings (), performanceLogThresholdMs, chain, log, logPerformance) :> IViewModel<_>)
            |> ObservableCollection
          { SubModelSeqUnkeyedData = d
            Vms = vms }
          |> SubModelSeqUnkeyed
          |> Some
      | SubModelSeqKeyedData d ->
          let d = d |> BindingData.SubModelSeqKeyed.measureFunctions measure measure measure2 measure
          let toMsg = fun msg -> d.ToMsg (getCurrentModel ()) msg
          let vms =
            d.GetSubModels initialModel
            |> Seq.map (fun m ->
                 let mId = d.GetId m
                 let chain = getNameChainForItem name (mId |> string)
                 DynamicViewModel(m, (fun msg -> toMsg (mId, msg) |> dispatch), d.GetBindings (), performanceLogThresholdMs, chain, log, logPerformance) :> IViewModel<_>)
            |> ObservableCollection
          { SubModelSeqKeyedData = d
            Vms = vms }
          |> SubModelSeqKeyed
          |> Some
      | SubModelSelectedItemData d ->
          let d = d |> BindingData.SubModelSelectedItem.measureFunctions measure measure2
          d.SubModelSeqBindingName
          |> getFunctionsForSubModelSelectedItem
          |> Option.map (fun (getId, fromId) ->
              { Get = d.Get
                Set = fun obj m -> d.Set obj m |> dispatch
                SubModelSeqBindingName = d.SubModelSeqBindingName
                GetId = getId
                FromId = fromId }
              |> SubModelSelectedItem)

  member this.Recursive<'model, 'msg>
      (initialModel: 'model,
       getCurrentModel: unit -> 'model,
       dispatch: 'msg -> unit,
       binding: BindingData<'model, 'msg>)
      : VmBinding<'model, 'msg> option =
    option {
      match binding with
      | BaseBindingData d ->
          let! b = this.Base(initialModel, getCurrentModel, dispatch, d)
          return BaseVmBinding b
      | CachingData d ->
          let! b = this.Recursive(initialModel, getCurrentModel, dispatch, d)
          return b.AddCaching
      | ValidationData d ->
          let d = d |> BindingData.Validation.measureFunctions measure
          let! b = this.Recursive(initialModel, getCurrentModel, dispatch, d.BindingData)
          return b.AddValidation (getCurrentModel ()) d.Validate
      | LazyData d ->
          let d = d |> BindingData.Lazy.measureFunctions measure
          let! b = this.Recursive(initialModel, getCurrentModel, dispatch, d.BindingData)
          return b.AddLazy d.Equals
      | AlterMsgStreamData d ->
          let initialModel' : obj = d.Get initialModel
          let getCurrentModel' : unit -> obj = getCurrentModel >> d.Get
          let dispatch' : obj -> unit = d.CreateFinalDispatch(getCurrentModel, dispatch)
          let! b = this.Recursive(initialModel', getCurrentModel', dispatch', d.BindingData)
          return { Binding = b
                   Get = d.Get
                   Dispatch = dispatch' }
                 |> AlterMsgStream
    }


/// Updates the binding and returns a list indicating what events to raise for this binding
and internal Update
    (name: string,
     nameChain: string,
     getNameChainFor: string -> string,
     getNameChainForItem: string -> string -> string,
     performanceLogThresholdMs: int,
     log: ILogger,
     logPerformance: ILogger) =

  member _.Base<'model, 'msg>
      (currentModel: 'model,
       newModel: 'model,
       dispatch: 'msg -> unit,
       binding: BaseVmBinding<'model, 'msg>) =
    match binding with
      | OneWay _
      | TwoWay _
      | SubModelSelectedItem _ -> [ PropertyChanged name ]
      | OneWayToSource _ -> []
      | OneWaySeq b ->
          b.OneWaySeqData.Merge(b.Values, currentModel, newModel)
          []
      | Cmd cmd -> cmd |> CanExecuteChanged |> List.singleton
      | SubModel b ->
        let d = b.SubModelData
        match b.Vm.Value, d.GetModel newModel with
        | ValueNone, ValueNone -> []
        | ValueSome _, ValueNone ->
            b.Vm.Value <- ValueNone
            [ PropertyChanged name ]
        | ValueNone, ValueSome m ->
            let toMsg = fun msg -> d.ToMsg currentModel msg
            b.Vm.Value <-
                DynamicViewModel(m, toMsg >> dispatch, d.GetBindings (), performanceLogThresholdMs, getNameChainFor name, log, logPerformance)
                :> IViewModel<_>
                |> ValueSome
            [ PropertyChanged name ]
        | ValueSome vm, ValueSome m ->
            vm.UpdateModel m
            []
      | SubModelVm b ->
        let d = b.SubModelVmData
        match b.Vm.Value, d.GetModel newModel with
        | ValueNone, ValueNone -> []
        | ValueSome _, ValueNone ->
            b.Vm.Value <- ValueNone
            [ PropertyChanged name ]
        | ValueNone, ValueSome m ->
            let toMsg = fun msg -> d.ToMsg currentModel msg
            b.Vm.Value <- ValueSome <| d.CreateViewModel(m, toMsg >> dispatch)
            [ PropertyChanged name ]
        | ValueSome vm, ValueSome m ->
            vm.UpdateModel m
            []
      | SubModelWin b ->
          let d = b.SubModelWinData
          let winPropChain = getNameChainFor name
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

          let newVm model : ViewModel<_, _> =
            let toMsg = fun msg -> d.ToMsg currentModel msg
            DynamicViewModel(model, toMsg >> dispatch, d.GetBindings (), performanceLogThresholdMs, getNameChainFor name, log, logPerformance)
            :> ViewModel<_, _>

          match b.VmWinState.Value, d.GetState newModel with
          | WindowState.Closed, WindowState.Closed ->
              []
          | WindowState.Hidden vm, WindowState.Hidden m
          | WindowState.Visible vm, WindowState.Visible m ->
              vm.UpdateModel m
              []
          | WindowState.Hidden _, WindowState.Closed
          | WindowState.Visible _, WindowState.Closed ->
              close ()
              b.VmWinState.Value <- WindowState.Closed
              [ PropertyChanged name ]
          | WindowState.Visible vm, WindowState.Hidden m ->
              hide ()
              vm.UpdateModel m
              b.VmWinState.Value <- WindowState.Hidden vm
              []
          | WindowState.Hidden vm, WindowState.Visible m ->
              vm.UpdateModel m
              showHidden ()
              b.VmWinState.Value <- WindowState.Visible vm
              []
          | WindowState.Closed, WindowState.Hidden m ->
              let vm = newVm m
              log.LogTrace("[{BindingNameChain}] Creating hidden window", winPropChain)
              showNew vm Visibility.Hidden (fun () -> currentModel) dispatch
              b.VmWinState.Value <- vm :> IViewModel<_> |> WindowState.Hidden
              [ PropertyChanged name ]
          | WindowState.Closed, WindowState.Visible m ->
              let vm = newVm m
              log.LogTrace("[{BindingNameChain}] Creating visible window", winPropChain)
              showNew vm Visibility.Visible (fun () -> currentModel) dispatch
              b.VmWinState.Value <- vm :> IViewModel<_> |> WindowState.Visible
              [ PropertyChanged name ]
      | SubModelSeqUnkeyed b ->
          let d = b.SubModelSeqUnkeyedData
          let create m idx : IViewModel<_> =
            let toMsg = fun msg -> d.ToMsg currentModel msg
            let chain = getNameChainForItem name (idx |> string)
            DynamicViewModel(m, (fun msg -> toMsg (idx, msg) |> dispatch), d.GetBindings (), performanceLogThresholdMs, chain, log, logPerformance)
            :> IViewModel<_>
          let update (vm: IViewModel<_>) = (vm :> IViewModel<obj>).UpdateModel
          Merge.unkeyed create update b.Vms (d.GetModels newModel)
          []
      | SubModelSeqKeyed b ->
          let d = b.SubModelSeqKeyedData
          let getTargetId getId (vm: IViewModel<_>) = getId vm.CurrentModel
          let create m id : IViewModel<_> =
            let toMsg = fun msg -> d.ToMsg currentModel msg
            let chain = getNameChainForItem name (id |> string)
            DynamicViewModel(m, (fun msg -> toMsg (id, msg) |> dispatch), d.GetBindings (), performanceLogThresholdMs, chain, log, logPerformance)
            :> IViewModel<_>
          let update (vm: IViewModel<_>) = (vm :> IViewModel<obj>).UpdateModel
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

  member this.Recursive<'model, 'msg>
      (currentModel: 'model,
       newModel: 'model,
       dispatch: 'msg -> unit,
       binding: VmBinding<'model, 'msg>)
      : UpdateData list =
    match binding with
      | BaseVmBinding b -> this.Base(currentModel, newModel, dispatch, b)
      | Cached b ->
          let updates = this.Recursive(currentModel, newModel, dispatch, b.Binding)
          updates
          |> List.filter UpdateData.isPropertyChanged
          |> List.iter (fun _ -> b.Cache.Value <- None)
          updates
      | Validatation b ->
          let updates = this.Recursive(currentModel, newModel, dispatch, b.Binding)
          let newErrors = b.Validate newModel
          if b.Errors.Value <> newErrors then
            b.Errors.Value <- newErrors
            ErrorsChanged name :: updates
          else
            updates
      | Lazy b ->
          if b.Equals currentModel newModel then
            []
          else
            this.Recursive(currentModel, newModel, dispatch, b.Binding)
      | AlterMsgStream b ->
          this.Recursive(b.Get currentModel, b.Get newModel, b.Dispatch, b.Binding)


and internal Get(nameChain: string) =

  member _.Base (model: 'model, binding: BaseVmBinding<'model, 'msg>) =
    match binding with
    | OneWay { OneWayData = d } -> d.Get model |> Ok
    | TwoWay b -> b.Get model |> Ok
    | OneWayToSource _ -> GetError.OneWayToSource |> Error
    | OneWaySeq { Values = vals } -> vals |> box |> Ok
    | Cmd cmd -> cmd |> box |> Ok
    | SubModel { Vm = vm } -> vm.Value |> ValueOption.toObj |> box |> Ok
    | SubModelVm { Vm = vm } -> vm.Value |> ValueOption.toObj |> box |> Ok
    | SubModelWin { VmWinState = vm } ->
        vm.Value
        |> WindowState.toVOption
        |> ValueOption.map box
        |> ValueOption.toObj
        |> Ok
    | SubModelSeqUnkeyed { Vms = vms }
    | SubModelSeqKeyed { Vms = vms } -> vms |> box |> Ok
    | SubModelSelectedItem b ->
        b.TryGetMember model
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


  member this.Recursive<'model, 'msg>
      (model: 'model,
       binding: VmBinding<'model, 'msg>)
      : Result<obj, GetError> =
    match binding with
    | BaseVmBinding b -> this.Base(model, b)
    | Cached b ->
        match b.Cache.Value with
        | Some v -> v |> Ok
        | None ->
            let x = this.Recursive(model, b.Binding)
            x |> Result.iter (fun v -> b.Cache.Value <- Some v)
            x
    | Validatation b -> this.Recursive(model, b.Binding)
    | Lazy b -> this.Recursive(model, b.Binding)
    | AlterMsgStream b -> this.Recursive(b.Get model, b.Binding)


and internal Set(value: obj) =

  member _.Base(model: 'model, binding: BaseVmBinding<'model, 'msg>) =
    match binding with
    | TwoWay b ->
        b.Set value model
        true
    | OneWayToSource b ->
        b.Set value model
        true
    | SubModelSelectedItem b ->
        let bindingModel =
          (value :?> IViewModel<obj>)
          |> ValueOption.ofObj
          |> ValueOption.map (fun vm -> vm.CurrentModel)
        b.TrySetMember(model, bindingModel)
        true
    | OneWay _
    | OneWaySeq _
    | Cmd _
    | SubModel _
    | SubModelVm _
    | SubModelWin _
    | SubModelSeqUnkeyed _
    | SubModelSeqKeyed _ ->
        false

  member this.Recursive<'model, 'msg>(model: 'model, binding: VmBinding<'model, 'msg>) : bool =
    match binding with
    | BaseVmBinding b -> this.Base(model, b)
    | Cached b ->
        let successful = this.Recursive(model, b.Binding)
        if successful then
          b.Cache.Value <- None  // TODO #185: write test
        successful
    | Validatation b -> this.Recursive(model, b.Binding)
    | Lazy b -> this.Recursive(model, b.Binding)
    | AlterMsgStream b -> this.Recursive(b.Get model, b.Binding)


and [<AllowNullLiteral>] [<AbstractClass>] ViewModel<'model, 'msg>
      ( initialModel: 'model,
        dispatch: 'msg -> unit,
        performanceLogThresholdMs: int,
        nameChain: string,
        log: ILogger,
        logPerformance: ILogger)
      as this =

  let mutable currentModel = initialModel

  let propertyChanged = Event<PropertyChangedEventHandler, PropertyChangedEventArgs>()
  let errorsChanged = DelegateEvent<EventHandler<DataErrorsChangedEventArgs>>()

  let validationErrors = Dictionary<string, string list ref>()
  let bindings = Dictionary<string, VmBinding<'model, 'msg>>()

  let raisePropertyChanged name =
    log.LogTrace("[{BindingNameChain}] PropertyChanged {BindingName}", nameChain, name)
    propertyChanged.Trigger(this, PropertyChangedEventArgs name)
  let raiseCanExecuteChanged (cmd: Command) =
    cmd.RaiseCanExecuteChanged ()
  let raiseErrorsChanged name =
    log.LogTrace("[{BindingNameChain}] ErrorsChanged {BindingName}", nameChain, name)
    errorsChanged.Trigger([| box this; box <| DataErrorsChangedEventArgs name |])
    

  member internal _.getNameChainFor name =
    sprintf "%s.%s" nameChain name
  member internal _.getNameChainForItem collectionBindingName itemId =
    sprintf "%s.%s.%s" nameChain collectionBindingName itemId

  member internal _.Bindings = bindings
  member internal _.ValidationErrors = validationErrors


  member _.CurrentModel : 'model = currentModel
  member internal _.UpdateModel (newModel: 'model) : unit =
    let eventsToRaise =
      bindings
      |> Seq.collect (fun (Kvp (name, binding)) -> Update(name, nameChain, this.getNameChainFor, this.getNameChainForItem, performanceLogThresholdMs, log, logPerformance).Recursive(currentModel, newModel, dispatch, binding))
      |> Seq.toList
    currentModel <- newModel
    eventsToRaise
    |> List.iter (function
      | ErrorsChanged name -> raiseErrorsChanged name
      | PropertyChanged name -> raisePropertyChanged name
      | CanExecuteChanged cmd -> cmd |> raiseCanExecuteChanged)

  
  interface IViewModel<'model> with
    member _.CurrentModel = this.CurrentModel
    member _.UpdateModel (newModel) = this.UpdateModel(newModel)

  interface IViewModel with
    member _.CurrentModel = this.CurrentModel |> box
    member _.UpdateModel (newModel) = newModel |> unbox |> this.UpdateModel

  interface INotifyPropertyChanged with
    [<CLIEvent>]
    member _.PropertyChanged = propertyChanged.Publish

  interface INotifyDataErrorInfo with
    [<CLIEvent>]
    member _.ErrorsChanged = errorsChanged.Publish
    member _.HasErrors =
      // WPF calls this too often, so don't log https://github.com/elmish/Elmish.WPF/issues/354
      validationErrors
      |> Seq.map (fun (Kvp(_, errors)) -> errors.Value)
      |> Seq.filter (not << List.isEmpty)
      |> (not << Seq.isEmpty)
    member _.GetErrors name =
      let name = name |> Option.ofObj |> Option.defaultValue "<null>" // entity-level errors are being requested when given null or ""  https://docs.microsoft.com/en-us/dotnet/api/system.componentmodel.inotifydataerrorinfo.geterrors#:~:text=null%20or%20Empty%2C%20to%20retrieve%20entity-level%20errors
      log.LogTrace("[{BindingNameChain}] GetErrors {BindingName}", nameChain, name)
      validationErrors
      |> IReadOnlyDictionary.tryFind name
      |> Option.map (fun errors -> errors.Value)
      |> Option.defaultValue []
      |> (fun x -> upcast x)
      
and [<AllowNullLiteral>] public ViewModelBase<'model, 'msg>(initialModel: 'model, dispatch: 'msg -> unit) as this =
  inherit ViewModel<'model, 'msg>(initialModel, dispatch, 0, "", NullLogger.Instance, NullLogger.Instance)

  let initializeGetBindingIfNew name getter =
    if this.Bindings.ContainsKey name |> not then
      let bindingData = BaseBindingData (OneWayData { Get = getter >> box })
      let vmBinding = Initialize(NullLogger.Instance, NullLogger.Instance, 0, name, this.getNameChainFor, this.getNameChainForItem, name, fun _ -> failwith "").Recursive(initialModel, (fun () -> this.CurrentModel), dispatch, bindingData)
      Option.iter (fun binding -> this.Bindings.Add(name, binding)) vmBinding
      
  let initializeSetBindingIfNew name setter =
    if this.Bindings.ContainsKey name |> not then
      let bindingData = BaseBindingData (OneWayToSourceData { Set = unbox >> setter })
      let vmBinding = Initialize(NullLogger.Instance, NullLogger.Instance, 0, name, this.getNameChainFor, this.getNameChainForItem, name, fun _ -> failwith "").Recursive(initialModel, (fun () -> this.CurrentModel), dispatch, bindingData)
      Option.iter (fun binding -> this.Bindings.Add(name, binding)) vmBinding

  let initializeCmdBindingIfNew name exec canExec =
    if this.Bindings.ContainsKey name |> not then
      let vmBinding = Command((fun p -> this.CurrentModel |> exec p |> ValueOption.iter dispatch), (fun p -> this.CurrentModel |> canExec p))
      this.Bindings.Add(name, BaseVmBinding (Cmd vmBinding))
      vmBinding
    else
      match this.Bindings.Item name with
      | BaseVmBinding (Cmd vmBinding) -> vmBinding
      | x -> failwithf "Wrong binding type found for %s, should be BaseVmBinding, found %A" name x

  let initializeSubModelBindingIfNew name (getModel: 'model -> 'bindingModel voption) (toMsg: 'model -> 'bindingMsg -> 'msg) (createViewModel: 'bindingModel * ('bindingMsg -> unit) -> 'viewModel) =
    if this.Bindings.ContainsKey name |> not then
      let binding =
        getModel initialModel
        |> ValueOption.map (fun m -> createViewModel(m, toMsg this.CurrentModel >> dispatch))
        |> (fun vm -> { SubModelVmData = { GetModel = getModel; ToMsg = toMsg; CreateViewModel = createViewModel }; Vm = ref vm })
      let toMsg2 = fun m bMsg -> binding.SubModelVmData.ToMsg m (unbox bMsg)
      let getModel2 = binding.SubModelVmData.GetModel >> ValueOption.map box
      let createViewModel2 = (fun (m,dispatch) -> (unbox m,box >> dispatch)) >> binding.SubModelVmData.CreateViewModel >> (fun x -> x :> IViewModel)
      let initialVm2 = getModel2 this.CurrentModel |> ValueOption.map (fun m -> createViewModel2 (m, toMsg2 this.CurrentModel >> dispatch))
      let vmBinding = { SubModelVmData = { GetModel = getModel2; ToMsg = toMsg2; CreateViewModel = createViewModel2 }; Vm = ref initialVm2 }
      do this.Bindings.Add(name, BaseVmBinding (SubModelVm vmBinding))
    
    match this.Bindings.Item name with
    | BaseVmBinding (SubModelVm vmBinding) -> vmBinding.Vm.Value |> ValueOption.map (fun vm -> vm :?> 'viewModel)
    | x -> failwithf "Wrong binding type found for %s, should be BaseVmBinding, found %A" name x
    

  member _.getValue(getter: 'model -> 'a, [<CallerMemberName>] ?memberName: string) = Option.iter (fun name -> initializeGetBindingIfNew name getter) memberName; getter this.CurrentModel
  member _.setValue(setter: 'a -> 'model -> 'msg, v: 'a, [<CallerMemberName>] ?memberName: string) = Option.iter (fun name -> initializeSetBindingIfNew name setter) memberName; this.CurrentModel |> setter v |> dispatch
  member _.cmd(exec: obj -> 'model -> 'msg voption, canExec: obj -> 'model -> bool, [<CallerMemberName>] ?memberName: string) = Option.map (fun name -> initializeCmdBindingIfNew name exec canExec :> ICommand) memberName |> Option.defaultValue null;
  member _.subModel(getModel: 'model -> 'bindingModel voption, toMsg, createViewModel, [<CallerMemberName>] ?memberName: string) = memberName |> ValueOption.ofOption |> ValueOption.bind (fun name -> initializeSubModelBindingIfNew name getModel toMsg createViewModel) |> ValueOption.defaultValue null;


and DynamicViewModel<'model, 'msg>
      ( initialModel: 'model,
        dispatch: 'msg -> unit,
        bindings: Binding<'model, 'msg> list,
        performanceLogThresholdMs: int,
        nameChain: string,
        log: ILogger,
        logPerformance: ILogger)
        as this =
  inherit ViewModel<'model, 'msg>(initialModel, dispatch, performanceLogThresholdMs, nameChain, log, logPerformance)

  let initialize bindings =
    log.LogTrace("[{BindingNameChain}] Initializing bindings", nameChain)
    let getFunctionsForSubModelSelectedItem name =
      this.Bindings
      |> Dictionary.tryFind name
      |> function
        | Some b ->
          match FuncsFromSubModelSeqKeyed().Recursive(b) with
          | Some x -> Some x
          | None -> log.LogError("SubModelSelectedItem binding referenced binding {SubModelSeqBindingName} but it is not a SubModelSeq binding", name)
                    None
        | None -> log.LogError("SubModelSelectedItem binding referenced binding {SubModelSeqBindingName} but no binding was found with that name", name)
                  None
    let sortedBindings =
      bindings
      |> List.sortWith (SubModelSelectedItemLast().CompareBindings())
    for b in sortedBindings do
      if this.Bindings.ContainsKey b.Name then
        log.LogError("Binding name {BindingName} is duplicated. Only the first occurrence will be used.", b.Name)
      else
        Initialize(log, logPerformance, performanceLogThresholdMs, nameChain, this.getNameChainFor, this.getNameChainForItem, b.Name, getFunctionsForSubModelSelectedItem)
          .Recursive(initialModel, (fun () -> this.CurrentModel), (unbox >> dispatch), b.Data)
        |> Option.iter (fun binding ->
          this.Bindings.Add(b.Name, binding))
    this.Bindings
    |> Seq.map (Pair.ofKvp >> Pair.mapAll Some (FirstValidationErrors().Recursive) >> PairOption.sequence)
    |> SeqOption.somes
    |> Seq.iter this.ValidationErrors.Add

  do initialize bindings

  member _.TryGetMember (name) =
    log.LogTrace("[{BindingNameChain}] TryGetMember {BindingName}", nameChain, name)
    match this.Bindings.TryGetValue name with
    | false, _ ->
        log.LogError("[{BindingNameChain}] TryGetMember FAILED: Property {BindingName} doesn't exist", nameChain, name)
        null
    | true, binding ->
        try
          match Get(nameChain).Recursive(this.CurrentModel, binding) with
          | Ok v ->
              v
          | Error e ->
              match e with
              | GetError.OneWayToSource -> log.LogError("[{BindingNameChain}] TryGetMember FAILED: Binding {BindingName} is read-only", nameChain, name)
              | GetError.SubModelSelectedItem d -> log.LogError("[{BindingNameChain}] TryGetMember FAILED: Failed to find an element of the SubModelSeq binding {SubModelSeqBindingName} with ID {ID} in the getter for the binding {SubModelSelectedItemName}", d.NameChain, d.SubModelSeqBindingName, d.Id)
              null
        with e ->
          log.LogError(e, "[{BindingNameChain}] TryGetMember FAILED: Exception thrown while processing binding {BindingName}", nameChain, name)
          reraise ()

  member _.TrySetMember (name, value: obj) =
    log.LogTrace("[{BindingNameChain}] TrySetMember {BindingName}", nameChain, name)
    match this.Bindings.TryGetValue name with
    | false, _ ->
        log.LogError("[{BindingNameChain}] TrySetMember FAILED: Property {BindingName} doesn't exist", nameChain, name)
        obj()
    | true, binding ->
        try
          let success = Set(value).Recursive(this.CurrentModel, binding)
          if not success then
            log.LogError("[{BindingNameChain}] TrySetMember FAILED: Binding {BindingName} is read-only", nameChain, name)
          obj()
        with e ->
          log.LogError(e, "[{BindingNameChain}] TrySetMember FAILED: Exception thrown while processing binding {BindingName}", nameChain, name)
          reraise ()

  interface IDynamicMetaObjectProvider with
    member __.GetMetaObject(parameter) : DynamicMetaObject =
      DynamicViewModelMetaObject<'model, 'msg>(parameter, this)
      :> DynamicMetaObject

and DynamicViewModelMetaObject<'model, 'msg>(parameter: Expression, value: DynamicViewModel<'model, 'msg>) =
  inherit DynamicMetaObject(parameter, BindingRestrictions.Empty, value)

  override this.BindGetMember(binder) =
    let parameters : Expression array = [| Expression.Constant(binder.Name) |]

    let methodInfo = typeof<DynamicViewModel<'model, 'msg>>.GetMethod("TryGetMember")

    DynamicMetaObject(
      Expression.Call(
        Expression.Convert(this.Expression, this.LimitType),
        methodInfo,
        parameters),
      BindingRestrictions.GetTypeRestriction(this.Expression, this.LimitType))

  override this.BindSetMember(binder, value) =
    let parameters : Expression array = [|
      Expression.Constant(binder.Name)
      Expression.Convert(value.Expression, typeof<obj>) |]

    let methodInfo = typeof<DynamicViewModel<'model, 'msg>>.GetMethod("TrySetMember")
    
    DynamicMetaObject(
      Expression.Call(
        Expression.Convert(this.Expression, this.LimitType),
        methodInfo,
        parameters),
      BindingRestrictions.GetTypeRestriction(this.Expression, this.LimitType))
