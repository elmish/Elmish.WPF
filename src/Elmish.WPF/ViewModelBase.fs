namespace Elmish.WPF

open System
open System.Collections.Generic
open System.ComponentModel
open System.Runtime.CompilerServices
open System.Windows.Input
open Microsoft.Extensions.Logging


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
    name (getModel: 'model -> 'bindingModel voption)
    (toMsg: 'model -> 'bindingMsg -> 'msg)
    (createViewModel: ViewModelArgs<'bindingModel, 'bindingMsg> -> #ViewModelBase<'bindingModel, 'bindingMsg>) =
    if bindings.ContainsKey name |> not then
      let binding =
        getModel initialModel
        |> ValueOption.map (fun m -> createViewModel(ViewModelArgs.create m (toMsg currentModel >> dispatch) name loggingArgs))
        |> (fun vm -> { SubModelData = { GetModel = getModel; ToMsg = toMsg; CreateViewModel = createViewModel; UpdateViewModel = (fun (vm,m) -> vm.UpdateModel(m)) }; Vm = ref vm })
      let toMsg2 = fun m bMsg -> binding.SubModelData.ToMsg m (unbox bMsg)
      let getModel2 = binding.SubModelData.GetModel >> ValueOption.map box
      let createViewModel2 = (fun args -> ViewModelArgs.map unbox box args |> binding.SubModelData.CreateViewModel |> box)
      let updateViewModel2 = fun (vm,m) -> binding.SubModelData.UpdateViewModel(unbox vm, unbox m)
      let initialVm2 = getModel2 currentModel |> ValueOption.map (fun m -> createViewModel2 (ViewModelArgs.create m (toMsg2 currentModel >> dispatch) name loggingArgs))
      let vmBinding = { SubModelData = { GetModel = getModel2; ToMsg = toMsg2; CreateViewModel = createViewModel2; UpdateViewModel = updateViewModel2 }; Vm = ref initialVm2 }
      do bindings.Add(name, BaseVmBinding (SubModel vmBinding))
    
    match bindings.Item name with
    | BaseVmBinding (SubModel vmBinding) -> vmBinding.Vm.Value |> ValueOption.map (fun vm -> vm :?> 'viewModel)
    | foundBinding -> log.LogError("Wrong binding type found for {name}, should be BaseVmBinding, found {foundBinding}", name, foundBinding); ValueNone

  member _.getValue(getter: 'model -> 'a, [<CallerMemberName>] ?memberName: string) =
    Option.iter (fun name -> initializeGetBindingIfNew name getter) memberName
    getter currentModel

  member _.setValue(setter: 'model -> 'msg, [<CallerMemberName>] ?memberName: string) =
    Option.iter (fun name -> initializeSetBindingIfNew name setter) memberName
    currentModel |> setter |> dispatch

  member _.cmd(exec: obj -> 'model -> 'msg voption, canExec: obj -> 'model -> bool, [<CallerMemberName>] ?memberName: string) =
    Option.bind (fun name -> initializeCmdBindingIfNew name exec canExec) memberName |> Option.defaultValue null

  member _.subModel(getModel: 'model -> 'bindingModel voption, toMsg, createViewModel, [<CallerMemberName>] ?memberName: string) =
    memberName |> ValueOption.ofOption |> ValueOption.bind (fun name -> initializeSubModelBindingIfNew name getModel toMsg createViewModel) |> ValueOption.defaultValue null

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
