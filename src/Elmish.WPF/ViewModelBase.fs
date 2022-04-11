namespace Elmish.WPF

open System
open System.Collections.Generic
open System.ComponentModel
open System.Runtime.CompilerServices
open System.Windows.Input
open Microsoft.Extensions.Logging
open System.Collections.ObjectModel


type [<AllowNullLiteral>] ViewModelBase<'model,'msg>
      ( args: ViewModelArgs<'model, 'msg>,
        getSender: unit -> obj) =

  let { initialModel = initialModel
        dispatch = dispatch
        loggingArgs = loggingArgs
      } = args

  let { log = log
        nameChain = nameChain
      } = loggingArgs

  let mutable currentModel = initialModel

  let propertyChanged = Event<PropertyChangedEventHandler, PropertyChangedEventArgs>()
  let errorsChanged = DelegateEvent<EventHandler<DataErrorsChangedEventArgs>>()

  let bindings = Dictionary<String, VmBinding<'model, 'msg>>()
  let validationErrors = Dictionary<String, String list ref>()

  let raisePropertyChanged name =
    log.LogTrace("[{BindingNameChain}] PropertyChanged {BindingName}", nameChain, name)
    propertyChanged.Trigger(getSender (), PropertyChangedEventArgs name)
  let raiseCanExecuteChanged (cmd: Command) =
    cmd.RaiseCanExecuteChanged ()
  let raiseErrorsChanged name =
    log.LogTrace("[{BindingNameChain}] ErrorsChanged {BindingName}", nameChain, name)
    errorsChanged.Trigger([| getSender (); box <| DataErrorsChangedEventArgs name |])

  let initializeGetBindingIfNew name getter =
    if bindings.ContainsKey name |> not then
      let binding = BaseVmBinding (OneWay { OneWayData = { Get = getter >> box } })
      bindings.Add(name, binding)
      
  let initializeSetBindingIfNew name (setter: 'model -> 'msg) =
    if bindings.ContainsKey name |> not then
      let binding = BaseVmBinding (OneWayToSource { Set = (fun _ -> unbox >> setter >> dispatch) })
      bindings.Add(name, binding)

  let initializeCmdBindingIfNew name exec canExec =
    if bindings.ContainsKey name |> not then
      let vmBinding = Command((fun p -> currentModel |> exec p |> ValueOption.iter dispatch), (fun p -> currentModel |> canExec p))
      bindings.Add(name, BaseVmBinding (Cmd vmBinding))
      vmBinding :> ICommand |> Some
    else
      match bindings.Item name with
      | BaseVmBinding (Cmd vmBinding) -> vmBinding :> ICommand |> Some
      | foundBinding -> log.LogError("Wrong binding type found for {name}, should be BaseVmBinding, found {foundBinding}", name, foundBinding); None

  let initializeSubModelBindingIfNew
    name
    (getModel: 'model -> 'bindingModel voption)
    (toMsg: 'model -> 'bindingMsg -> 'msg)
    (createViewModel: ViewModelArgs<'bindingModel, 'bindingMsg> -> 'bindingViewModel)
    (updateViewModel: 'bindingViewModel * 'bindingModel -> unit) =
    if bindings.ContainsKey name |> not then
      let bindingData = { GetModel = getModel; ToMsg = toMsg; CreateViewModel = createViewModel; UpdateViewModel = updateViewModel }
      let bindingData2 = Binding.SubModel.mapMinorTypes box box box unbox unbox unbox bindingData
      let wrappedBindingData = bindingData2 |> SubModelData |> BaseBindingData
      let binding = Initialize(loggingArgs, name, fun _ -> None).Recursive(initialModel, dispatch, (fun () -> currentModel), wrappedBindingData)
      do binding |> Option.map (fun binding -> bindings.Add(name, binding)) |> ignore
      
    let binding = Get(nameChain).Recursive(currentModel, bindings.Item name)
    match binding with
    | Ok o -> o |> unbox<'viewModel> |> ValueSome
    | Error error -> log.LogError("Wrong binding type found for {name}, should be BaseVmBinding, found {foundBinding}", name, error); ValueNone

  let initializeSubModelSeqUnkeyedBindingIfNew
    name
    (getModels: 'model -> 'bindingModel seq)
    (toMsg: 'model -> int * 'bindingMsg -> 'msg)
    (createViewModel: ViewModelArgs<'bindingModel, 'bindingMsg> -> 'bindingViewModel)
    (updateViewModel: 'bindingViewModel * 'bindingModel -> unit) =
    if bindings.ContainsKey name |> not then
      let bindingData = { GetModels = getModels; ToMsg = toMsg; CreateViewModel = createViewModel; CreateCollection = ObservableCollection >> CollectionTarget.create; UpdateViewModel = updateViewModel }
      let bindingData2 = BindingData.SubModelSeqUnkeyed.box bindingData
      let wrappedBindingData = bindingData2 |> SubModelSeqUnkeyedData |> BaseBindingData
      let binding = Initialize(loggingArgs, name, fun _ -> None).Recursive(initialModel, dispatch, (fun () -> currentModel), wrappedBindingData)
      do binding |> Option.map (fun binding -> bindings.Add(name, binding)) |> ignore

    let binding = Get(nameChain).Recursive(currentModel, bindings.Item name)
    match binding with
    | Ok o -> o |> unbox<ObservableCollection<'bindingViewModel>> |> ValueSome
    | Error error -> log.LogError("Wrong binding type found for {name}, should be BaseVmBinding, found {foundBinding}", name, error); ValueNone

  let initializeSubModelSeqKeyedBindingIfNew
    name
    (getModels: 'model -> 'bindingModel seq)
    (getKey: 'bindingModel -> 'a)
    (toMsg: 'model -> 'a * 'bindingMsg -> 'msg)
    (createViewModel: ViewModelArgs<'bindingModel, 'bindingMsg> -> 'bindingViewModel)
    (updateViewModel: 'bindingViewModel * 'bindingModel -> unit)
    (getUnderlyingModel: 'bindingViewModel -> 'bindingModel) =
    if bindings.ContainsKey name |> not then
      let bindingData = { GetSubModels = getModels; ToMsg = toMsg; CreateViewModel = createViewModel; CreateCollection = ObservableCollection >> CollectionTarget.create; GetUnderlyingModel = getUnderlyingModel; UpdateViewModel = updateViewModel; GetId = getKey }
      let bindingData2 = BindingData.SubModelSeqKeyed.box bindingData
      let wrappedBindingData = bindingData2 |> SubModelSeqKeyedData |> BaseBindingData
      let binding = Initialize(loggingArgs, name, fun _ -> None).Recursive(initialModel, dispatch, (fun () -> currentModel), wrappedBindingData)
      do binding |> Option.map (fun binding -> bindings.Add(name, binding)) |> ignore

    let binding = Get(nameChain).Recursive(currentModel, bindings.Item name)
    match binding with
    | Ok o -> o |> unbox<ObservableCollection<'bindingViewModel>> |> ValueSome
    | Error error -> log.LogError("Wrong binding type found for {name}, should be BaseVmBinding, found {foundBinding}", name, error); ValueNone

  member _.getValue(getter: 'model -> 'a, [<CallerMemberName>] ?memberName: string) =
    Option.iter (fun name -> initializeGetBindingIfNew name getter) memberName
    currentModel |> getter

  member _.setValue(setter: 'model -> 'msg, [<CallerMemberName>] ?memberName: string) =
    Option.iter (fun name -> initializeSetBindingIfNew name setter) memberName
    currentModel |> setter |> dispatch

  member _.cmd(exec: obj -> 'model -> 'msg voption, canExec: obj -> 'model -> bool, [<CallerMemberName>] ?memberName: string) =
    Option.bind (fun name -> initializeCmdBindingIfNew name exec canExec) memberName |> Option.defaultValue null

  member _.subModel(getModel: 'model -> 'bindingModel voption, toMsg, createViewModel, updateViewModel, [<CallerMemberName>] ?memberName: string) =
    memberName |> ValueOption.ofOption |> ValueOption.bind (fun name -> initializeSubModelBindingIfNew name getModel toMsg createViewModel updateViewModel) |> ValueOption.defaultValue null

  member _.subModelSeqUnkeyed(getModels: 'model -> 'bindingModel seq, toMsg, createViewModel, updateViewModel, [<CallerMemberName>] ?memberName: string) =
    memberName |> ValueOption.ofOption |> ValueOption.bind (fun name -> initializeSubModelSeqUnkeyedBindingIfNew name getModels toMsg createViewModel updateViewModel) |> ValueOption.defaultValue null

  member _.subModelSeqKeyed(getModels: 'model -> 'bindingModel seq, toMsg, getKey, createViewModel, updateViewModel, getUnderlyingModel, [<CallerMemberName>] ?memberName: string) =
    memberName |> ValueOption.ofOption |> ValueOption.bind (fun name -> initializeSubModelSeqKeyedBindingIfNew name getModels toMsg getKey createViewModel updateViewModel getUnderlyingModel) |> ValueOption.defaultValue null

  member internal _.CurrentModel : 'model = currentModel

  member internal _.UpdateModel (newModel: 'model) : unit =
    let eventsToRaise =
      bindings
      |> Seq.collect (fun (Kvp (name, binding)) -> Update(loggingArgs, name).Recursive(currentModel, newModel, dispatch, binding))
      |> Seq.toList
    currentModel <- newModel
    eventsToRaise
    |> List.iter (function
      | ErrorsChanged name -> raiseErrorsChanged name
      | PropertyChanged name -> raisePropertyChanged name
      | CanExecuteChanged cmd -> cmd |> raiseCanExecuteChanged)

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

type ViewModelBase<'model, 'msg> with
  member this.subModel (getModel: 'model -> 'bindingModel voption, toMsg, createViewModel: ViewModelArgs<'bindingModel, 'bindingMsg> -> #ViewModelBase<'bindingModel, 'bindingMsg>, [<CallerMemberName>] ?memberName: string) =
    this.subModel (getModel, toMsg, createViewModel, (fun (vm,m) -> vm.UpdateModel(m)), ?memberName = memberName)

  member this.subModelBindings (getModel: 'model -> 'bindingModel voption, toMsg, bindings, [<CallerMemberName>] ?memberName: string) =
    this.subModel (getModel, toMsg, (fun args -> ViewModel<'bindingModel, 'bindingMsg>(args, bindings)), (fun (vm,m) -> vm.UpdateModel(m)), ?memberName = memberName)

  member this.subModelSeqUnkeyed (getModels: 'model -> 'bindingModel seq, toMsg, createViewModel: ViewModelArgs<'bindingModel, 'bindingMsg> -> #ViewModelBase<'bindingModel, 'bindingMsg>, [<CallerMemberName>] ?memberName: string) =
    this.subModelSeqUnkeyed (getModels, toMsg, createViewModel, (fun (vm,m) -> vm.UpdateModel(m)), ?memberName = memberName)

  member this.subModelSeqUnkeyedBindings (getModels: 'model -> 'bindingModel seq, toMsg, bindings, [<CallerMemberName>] ?memberName: string) =
    this.subModelSeqUnkeyed (getModels, toMsg, (fun args -> ViewModel<'bindingModel, 'bindingMsg>(args, bindings)), (fun (vm,m) -> vm.UpdateModel(m)), ?memberName = memberName)

  member this.subModelSeqKeyed (getModels: 'model -> 'bindingModel seq, getKey, toMsg, createViewModel: ViewModelArgs<'bindingModel, 'bindingMsg> -> #ViewModelBase<'bindingModel, 'bindingMsg>, [<CallerMemberName>] ?memberName: string) =
    this.subModelSeqKeyed (getModels, getKey, toMsg, createViewModel, (fun (vm,m) -> vm.UpdateModel(m)), (fun vm -> vm.CurrentModel), ?memberName = memberName)

  member this.subModelSeqKeyedBindings (getModels: 'model -> 'bindingModel seq, getKey, toMsg, bindings, [<CallerMemberName>] ?memberName: string) =
    this.subModelSeqKeyed (getModels, getKey, toMsg, (fun args -> ViewModel<'bindingModel, 'bindingMsg>(args, bindings)), (fun (vm,m) -> vm.UpdateModel(m)), (fun vm -> vm.CurrentModel), ?memberName = memberName)

module BindingBase =
  module SubModelBase =
    open Binding
    open Binding.SubModel

    /// <summary>
    ///   Creates a binding to a sub-model/component. You typically bind this
    ///   to the <c>DataContext</c> of a <c>UserControl</c> or similar.
    /// </summary>
    /// <param name="bindings">Returns the bindings for the sub-model.</param>
    let vopt (create: ViewModelArgs<'model, 'msg> -> #ViewModelBase<'model,'msg>)
        : string -> Binding<'model voption, 'msg> =
      { GetModel = id
        CreateViewModel = create
        UpdateViewModel = fun (vm,m) -> vm.UpdateModel(m)
        ToMsg = fun _ -> id }
      |> mapMinorTypes box box box unbox unbox unbox
      |> SubModelData
      |> BaseBindingData
      |> createBinding

    /// <summary>
    ///   Creates a binding to a sub-model/component. You typically bind this
    ///   to the <c>DataContext</c> of a <c>UserControl</c> or similar.
    /// </summary>
    /// <param name="bindings">Returns the bindings for the sub-model.</param>
    let opt (create: ViewModelArgs<'model, 'msg> -> #ViewModelBase<'model,'msg>)
        : string -> Binding<'model option, 'msg> =
      vopt create
      >> mapModel ValueOption.ofOption

    /// <summary>
    ///   Creates a binding to a sub-model/component. You typically bind this
    ///   to the <c>DataContext</c> of a <c>UserControl</c> or similar.
    /// </summary>
    /// <param name="bindings">Returns the bindings for the sub-model.</param>
    let required (create: ViewModelArgs<'model, 'msg> -> #ViewModelBase<'model,'msg>)
        : string -> Binding<'model, 'msg> =
      vopt create
      >> mapModel ValueSome

  module SubModelSeqUnkeyedBase =

    /// <summary>
    ///   Creates a binding to a sub-model/component. You typically bind this
    ///   to the <c>DataContext</c> of a <c>UserControl</c> or similar.
    /// </summary>
    /// <param name="bindings">Returns the bindings for the sub-model.</param>
    let required (create: ViewModelArgs<'model, 'msg> -> #ViewModelBase<'model,'msg>)
        : string -> Binding<'model seq, int * 'msg> =
      BindingData.SubModelSeqUnkeyed.create
        create
        (fun (vm,m) -> vm.UpdateModel(m))

  module SubModelSeqKeyedBase =

    /// <summary>
    ///   Creates a binding to a sub-model/component. You typically bind this
    ///   to the <c>DataContext</c> of a <c>UserControl</c> or similar.
    /// </summary>
    /// <param name="bindings">Returns the bindings for the sub-model.</param>
    let required (create: ViewModelArgs<'model, 'msg> -> #ViewModelBase<'model,'msg>) (getId: 'model -> 'id)
        : string -> Binding<'model seq, 'id * 'msg> =
      BindingData.SubModelSeqKeyed.create
        create
        (fun (vm,m) -> vm.UpdateModel(m))
        (fun vm -> vm.CurrentModel)
        getId
