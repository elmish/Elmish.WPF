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
      Data: BindingData<'model, 'msg> }


[<AutoOpen>]
module internal Helpers =

  let createBinding data name =
    { Name = name
      Data = data }

  type SubModelSelectedItemLast with
    member this.CompareBindings() : Binding<'model, 'msg> -> Binding<'model, 'msg> -> int =
      fun a b -> this.Recursive(a.Data) - this.Recursive(b.Data)

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

  let mutable currentModel = initialModel

  let propertyChanged = Event<PropertyChangedEventHandler, PropertyChangedEventArgs>()
  let errorsChanged = DelegateEvent<EventHandler<DataErrorsChangedEventArgs>>()

  let raisePropertyChanged name =
    log.LogTrace("[{BindingNameChain}] PropertyChanged {BindingName}", nameChain, name)
    propertyChanged.Trigger(this, PropertyChangedEventArgs name)
  let raiseCanExecuteChanged (cmd: Command) =
    cmd.RaiseCanExecuteChanged ()
  let raiseErrorsChanged name =
    log.LogTrace("[{BindingNameChain}] ErrorsChanged {BindingName}", nameChain, name)
    errorsChanged.Trigger([| box this; box <| DataErrorsChangedEventArgs name |])

  let (bindings, validationErrors) =
    log.LogTrace("[{BindingNameChain}] Initializing bindings", nameChain)
    let bindingDict = Dictionary<string, VmBinding<'model, 'msg>>(bindings.Length)
    let getFunctionsForSubModelSelectedItem name =
      bindingDict
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
      if bindingDict.ContainsKey b.Name then
        log.LogError("Binding name {BindingName} is duplicated. Only the first occurrence will be used.", b.Name)
      else
        Initialize(loggingArgs, b.Name, getFunctionsForSubModelSelectedItem)
          .Recursive(initialModel, (unbox >> dispatch), (fun () -> currentModel), b.Data)
        |> Option.iter (fun binding ->
          bindingDict.Add(b.Name, binding))
    let validationDict = Dictionary<string, string list ref>()
    bindingDict
    |> Seq.map (Pair.ofKvp >> Pair.mapAll Some (FirstValidationErrors().Recursive) >> PairOption.sequence)
    |> SeqOption.somes
    |> Seq.iter validationDict.Add
    (bindingDict    :> IReadOnlyDictionary<_,_>,
     validationDict :> IReadOnlyDictionary<_,_>)


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
              | GetError.SubModelSelectedItem d -> log.LogError("[{BindingNameChain}] TryGetMember FAILED: Failed to find an element of the SubModelSeq binding {SubModelSeqBindingName} with ID {ID} in the getter for the binding {SubModelSelectedItemName}", d.NameChain, d.SubModelSeqBindingName, d.Id)
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