namespace Elmish.WPF

open System
open System.Dynamic
open System.Collections.Generic
open System.ComponentModel
open Microsoft.Extensions.Logging

open BindingVmHelpers

/// Represents all necessary data used to create a binding.
type Binding<'model, 'msg> =
  internal
    { Name: string
      Data: BindingData<'model, 'msg, obj> }


[<AutoOpen>]
module internal Helpers =

  let createBinding data name =
    { Name = name
      Data = data }

  type SubModelSelectedItemLast with
    member this.CompareBindings() : Binding<'model, 'msg> -> Binding<'model, 'msg> -> int =
      fun a b -> this.Recursive(a.Data) - this.Recursive(b.Data)

type [<AllowNullLiteral>] IViewModel<'model, 'msg> =
  abstract member CurrentModel: 'model
  abstract member UpdateModel: 'model -> unit

module internal IViewModel =
  let currentModel (vm: #IViewModel<'model, 'msg>) = vm.CurrentModel
  let updateModel (vm: #IViewModel<'model, 'msg>, m: 'model) = vm.UpdateModel(m)

type internal ViewModelHelper<'model, 'msg> =
  { GetSender: unit -> obj
    LoggingArgs: LoggingViewModelArgs
    Bindings: IReadOnlyDictionary<string, VmBinding<'model, 'msg, obj>>
    ValidationErrors: IReadOnlyDictionary<string, string list ref>
    PropertyChanged: Event<PropertyChangedEventHandler, PropertyChangedEventArgs>
    ErrorsChanged: DelegateEvent<EventHandler<DataErrorsChangedEventArgs>> }

  interface INotifyPropertyChanged with
    [<CLIEvent>]
    member x.PropertyChanged = x.PropertyChanged.Publish

  interface INotifyDataErrorInfo with
    [<CLIEvent>]
    member x.ErrorsChanged = x.ErrorsChanged.Publish
    member x.HasErrors =
      // WPF calls this too often, so don't log https://github.com/elmish/Elmish.WPF/issues/354
      x.ValidationErrors
      |> Seq.map (fun (Kvp(_, errors)) -> errors.Value)
      |> Seq.filter (not << List.isEmpty)
      |> (not << Seq.isEmpty)
    member x.GetErrors name =
      let name = name |> Option.ofObj |> Option.defaultValue "<null>" // entity-level errors are being requested when given null or ""  https://docs.microsoft.com/en-us/dotnet/api/system.componentmodel.inotifydataerrorinfo.geterrors#:~:text=null%20or%20Empty%2C%20to%20retrieve%20entity-level%20errors
      x.LoggingArgs.log.LogTrace("[{BindingNameChain}] GetErrors {BindingName}", x.LoggingArgs.nameChain, name)
      x.ValidationErrors
      |> IReadOnlyDictionary.tryFind name
      |> Option.map (fun errors -> errors.Value)
      |> Option.defaultValue []
      |> (fun x -> upcast x)

module internal ViewModelHelper =

  let create getSender args bindings validationErrors ={
    GetSender = getSender
    LoggingArgs = args.loggingArgs
    ValidationErrors = validationErrors
    Bindings = bindings
    PropertyChanged = Event<PropertyChangedEventHandler, PropertyChangedEventArgs>()
    ErrorsChanged = DelegateEvent<EventHandler<DataErrorsChangedEventArgs>>()
  }

  let getEventsToRaise helper oldModel newModel =
    helper.Bindings
      |> Seq.collect (fun (Kvp (name, binding)) -> Update(helper.LoggingArgs, name).Recursive(oldModel, newModel, binding))
      |> Seq.toList

  let raiseEvents helper eventsToRaise =
    let {
      log = log
      nameChain = nameChain } = helper.LoggingArgs

    let raisePropertyChanged name =
      log.LogTrace("[{BindingNameChain}] PropertyChanged {BindingName}", nameChain, name)
      helper.PropertyChanged.Trigger(helper.GetSender (), PropertyChangedEventArgs name)
    let raiseCanExecuteChanged (cmd: Command) =
      cmd.RaiseCanExecuteChanged ()
    let raiseErrorsChanged name =
      log.LogTrace("[{BindingNameChain}] ErrorsChanged {BindingName}", nameChain, name)
      helper.ErrorsChanged.Trigger([| helper.GetSender (); box <| DataErrorsChangedEventArgs name |])
    
    eventsToRaise
    |> List.iter (function
      | ErrorsChanged name -> raiseErrorsChanged name
      | PropertyChanged name -> raisePropertyChanged name
      | CanExecuteChanged cmd -> cmd |> raiseCanExecuteChanged)

type [<AllowNullLiteral>] internal DynamicViewModel<'model, 'msg>
      ( args: ViewModelArgs<'model, 'msg>,
        bindings: Binding<'model, 'msg> list)
      as this =
  inherit DynamicObject()

  let { initialModel = initialModel
        dispatch = dispatch
        loggingArgs = loggingArgs
      } = args

  let { log = log
        nameChain = nameChain
      } = loggingArgs

  let mutable currentModel = args.initialModel

  let (bindings, validationErrors) =
    let getFunctionsForSubModelSelectedItem initializedBindings (name: string) =
      initializedBindings
      |> IReadOnlyDictionary.tryFind name
      |> function
        | Some b ->
          match FuncsFromSubModelSeqKeyed().Recursive(b) with
          | Some x -> Some x
          | None -> log.LogError("SubModelSelectedItem binding referenced binding {SubModelSeqBindingName} but it is not a SubModelSeq binding", name)
                    None
        | None -> log.LogError("SubModelSelectedItem binding referenced binding {SubModelSeqBindingName} but no binding was found with that name", name)
                  None

    let initializeBinding initializedBindings binding =
      Initialize(loggingArgs, binding.Name, getFunctionsForSubModelSelectedItem initializedBindings)
        .Recursive(initialModel, dispatch, (fun () -> currentModel), binding.Data)

    log.LogTrace("[{BindingNameChain}] Initializing bindings", nameChain)

    let bindingDict = Dictionary<string, VmBinding<'model, 'msg, obj>>(bindings.Length)
    let validationDict = Dictionary<string, string list ref>()

    let sortedBindings =
      bindings
      |> List.sortWith (SubModelSelectedItemLast().CompareBindings())
    for b in sortedBindings do
      if bindingDict.ContainsKey b.Name then
        log.LogError("Binding name {BindingName} is duplicated. Only the first occurrence will be used.", b.Name)
      else
        option {
          let! vmBinding = initializeBinding bindingDict b
          do bindingDict.Add(b.Name, vmBinding)
          let! errorList = FirstValidationErrors().Recursive(vmBinding)
          do validationDict.Add(b.Name, errorList)
          return ()
        } |> Option.defaultValue ()
    (bindingDict    :> IReadOnlyDictionary<_,_>,
     validationDict :> IReadOnlyDictionary<_,_>)

  let helper =
    ViewModelHelper.create
      (fun () -> this)
      args
      bindings
      validationErrors

  interface IViewModel<'model, 'msg> with
    member _.CurrentModel : 'model = currentModel

    member _.UpdateModel (newModel: 'model) : unit =
      let oldModel = currentModel
      currentModel <- newModel
      ViewModelHelper.getEventsToRaise helper oldModel newModel
      |> ViewModelHelper.raiseEvents helper

  override _.TryGetMember (binder, result) =
    log.LogTrace("[{BindingNameChain}] TryGetMember {BindingName}", nameChain, binder.Name)
    match bindings.TryGetValue binder.Name with
    | false, _ ->
        log.LogError("[{BindingNameChain}] TryGetMember FAILED: Property {BindingName} doesn't exist", nameChain, binder.Name)
        false
    | true, binding ->
        try
          match Get(nameChain).Recursive(currentModel, binding) with
          | Ok v ->
              result <- v
              true
          | Error e ->
              match e with
              | GetError.OneWayToSource -> log.LogError("[{BindingNameChain}] TryGetMember FAILED: Binding {BindingName} is read-only", nameChain, binder.Name)
              | GetError.SubModelSelectedItem d -> log.LogError("[{BindingNameChain}] TryGetMember FAILED: Failed to find an element of the SubModelSeq binding {SubModelSeqBindingName} with ID {ID} in the getter for the binding {BindingName}", d.NameChain, d.SubModelSeqBindingName, d.Id, binder.Name)
              | GetError.ToNullError (ValueOption.ToNullError.ValueCannotBeNull nonNullTypeName) -> log.LogError("[{BindingNameChain}] TryGetMember FAILED: Binding {BindingName} is null, but type {Type} is non-nullable", nameChain, binder.Name, nonNullTypeName)
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
          let success = Set(value).Recursive(currentModel, binding)
          if not success then
            log.LogError("[{BindingNameChain}] TrySetMember FAILED: Binding {BindingName} is read-only", nameChain, binder.Name)
          success
        with e ->
          log.LogError(e, "[{BindingNameChain}] TrySetMember FAILED: Exception thrown while processing binding {BindingName}", nameChain, binder.Name)
          reraise ()

  override _.GetDynamicMemberNames () =
    log.LogTrace("[{BindingNameChain}] GetDynamicMemberNames", nameChain)
    bindings.Keys


  interface INotifyPropertyChanged with
    [<CLIEvent>]
    member _.PropertyChanged = (helper :> INotifyPropertyChanged).PropertyChanged

  interface INotifyDataErrorInfo with
    [<CLIEvent>]
    member _.ErrorsChanged = (helper :> INotifyDataErrorInfo).ErrorsChanged
    member _.HasErrors = (helper :> INotifyDataErrorInfo).HasErrors
    member _.GetErrors name = (helper :> INotifyDataErrorInfo).GetErrors name
