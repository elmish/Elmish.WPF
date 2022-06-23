module Elmish.WPF.StaticViewModel

open System
open System.Collections.Generic
open System.ComponentModel
open System.Runtime.CompilerServices
open Microsoft.Extensions.Logging

open Elmish.WPF.BindingVmHelpers


type BindingT<'model, 'msg, 'a> =
  internal
    { Name: string
      DataT: BindingData<'model, 'msg, 'a> }

type StaticBindingT<'model, 'msg, 'a> = (string -> BindingT<'model, 'msg, 'a>)

type StaticHelper<'model, 'msg>(args: ViewModelArgs<'model, 'msg>, getSender: unit -> obj) =

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

  let mutable getBindings = Dictionary<String, VmBinding<'model, 'msg, obj>>()
  let mutable setBindings = Dictionary<String, VmBinding<'model, 'msg, obj>>()
  let mutable validationErrors = Dictionary<String, String list ref>()

  let raisePropertyChanged name =
    log.LogTrace("[{BindingNameChain}] PropertyChanged {BindingName}", nameChain, name)
    propertyChanged.Trigger(getSender (), PropertyChangedEventArgs name)
  let raiseCanExecuteChanged (cmd: Command) =
    cmd.RaiseCanExecuteChanged ()
  let raiseErrorsChanged name =
    log.LogTrace("[{BindingNameChain}] ErrorsChanged {BindingName}", nameChain, name)
    errorsChanged.Trigger([| getSender (); box <| DataErrorsChangedEventArgs name |])

  let getFunctionsForSubModelSelectedItem name : SelectedItemBinding<obj, obj, 'a, obj> option =
    getBindings
    |> Dictionary.tryFind name
    |> function
      | Some b ->
        let b = unbox<VmBinding<'model, 'msg, 'a>> b
        match FuncsFromSubModelSeqKeyed().Recursive(b) with
        | Some x ->
          Some x
        | None ->
          log.LogError("SubModelSelectedItem binding referenced binding {SubModelSeqBindingName} but it is not a SubModelSeq binding", name)
          None
      | None ->
        log.LogError("SubModelSelectedItem binding referenced binding {SubModelSeqBindingName} but no binding was found with that name", name)
        None

  member _.GetValue (binding: StaticBindingT<'model, 'msg, 'a>, [<CallerMemberName>] ?memberName: string) =
    option {
      let! name = memberName
      let binding = binding name

      let! b =
        option {
          if getBindings.ContainsKey name then
            return getBindings.Item name |> MapOutputType.recursivecase unbox box
          else
            //let binding = BindingData.mapVm box unbox binding
            let! b =
              Initialize(args.loggingArgs, name, getFunctionsForSubModelSelectedItem)
                .Recursive(currentModel, dispatch, (fun () -> currentModel), binding.DataT)
            do getBindings.Add (name, b |> MapOutputType.recursivecase box unbox)
            return b
          }
      let c = Get(nameChain).Recursive(currentModel, b)
      let! d =
        match c with
        | Ok x -> Some x
        | Error _ -> None
      return d
    } |> Option.defaultValue null

  member _.SetValue (value, [<CallerMemberName>] ?memberName: string) =
    fun (binding: StaticBindingT<'model, 'msg, 'a>) ->
      option {
        let! name = memberName
        let binding = binding name

        let! b =
          option {
            if setBindings.ContainsKey name then
              return setBindings.Item name |> MapOutputType.recursivecase unbox box
            else
              //let binding = BindingData.mapVm box unbox binding
              let! b =
                Initialize(args.loggingArgs, name, getFunctionsForSubModelSelectedItem)
                  .Recursive(currentModel, dispatch, (fun () -> currentModel), binding.DataT)
              do setBindings.Add (name, b |> MapOutputType.recursivecase box unbox)
              return b
            }
        let _c = Set(value).Recursive(currentModel, b)
        return ()
      } |> Option.defaultValue ()

  member _.CurrentModel : 'model = currentModel

  member _.UpdateModel (newModel: 'model) : unit =
    let eventsToRaise =
      getBindings
      |> Seq.collect (fun (Kvp (name, binding)) -> Update(loggingArgs, name).Recursive(ValueSome currentModel, (fun () -> currentModel), newModel, binding))
      |> Seq.toList
    currentModel <- newModel
    eventsToRaise
    |> List.iter (function
      | ErrorsChanged name -> name |> raiseErrorsChanged
      | PropertyChanged name -> name |> raisePropertyChanged
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

[<AllowNullLiteral>]
type ISubModel<'model, 'msg> =
  abstract StaticHelper: StaticHelper<'model, 'msg>

module StaticHelper =
  let create args getSender = StaticHelper(args, getSender)

module BindingT =
  let internal createStatic data name = { DataT = data; Name = name }
  open BindingData

  let internal mapData f binding =
    { DataT = binding.DataT |> f
      Name = binding.Name }

  /// Map the model of a binding via a contravariant mapping.
  let mapModel (f: 'a -> 'b) (binding: BindingT<'b, 'msg, 'vm>) = f |> mapModel |> mapData <| binding

  /// Map the message of a binding with access to the model via a covariant mapping.
  let mapMsgWithModel (f: 'a -> 'model -> 'b) (binding: BindingT<'model, 'a, 'vm>) = f |> mapMsgWithModel |> mapData <| binding

  /// Map the message of a binding via a covariant mapping.
  let mapMsg (f: 'a -> 'b) (binding: BindingT<'model, 'a, 'vm>) = f |> mapMsg |> mapData <| binding

  /// Set the message of a binding with access to the model.
  let SetMsgWithModel (f: 'model -> 'b) (binding: BindingT<'model, 'a, 'vm>) = f |> setMsgWithModel |> mapData <| binding

  /// Set the message of a binding.
  let setMsg (msg: 'b) (binding: BindingT<'model, 'a, 'vm>) = msg |> setMsg |> mapData <| binding


  /// Restrict the binding to models that satisfy the predicate after some model satisfies the predicate.
  let addSticky (predicate: 'model -> bool) (binding: BindingT<'model, 'msg, 'vm>) = predicate |> addSticky |> mapData <| binding

  /// <summary>
  ///   Adds caching to the given binding.  The cache holds a single value and
  ///   is invalidated after the given binding raises the
  ///   <c>PropertyChanged</c> event.
  /// </summary>
  /// <param name="binding">The binding to which caching is added.</param>
  let addCaching (binding: BindingT<'model, 'msg, 'vm>) : BindingT<'model, 'msg, 'vm> =
    binding
    |> mapData addCaching

  /// <summary>
  ///   Adds validation to the given binding using <c>INotifyDataErrorInfo</c>.
  /// </summary>
  /// <param name="validate">Returns the errors associated with the given model.</param>
  /// <param name="binding">The binding to which validation is added.</param>
  let addValidation (validate: 'model -> string list) (binding: BindingT<'model, 'msg, 'vm>) : BindingT<'model, 'msg, 'vm> =
    binding
    |> (addValidation validate |> mapData)

  /// <summary>
  ///   Adds laziness to the updating of the given binding. If the models are considered equal,
  ///   then updating of the given binding is skipped.
  /// </summary>
  /// <param name="equals">Updating skipped when this function returns <c>true</c>.</param>
  /// <param name="binding">The binding to which the laziness is added.</param>
  let addLazy (equals: 'model -> 'model -> bool) (binding: BindingT<'model, 'msg, 'vm>) : BindingT<'model, 'msg, 'vm> =
    binding
    |> (addLazy equals |> mapData)

  /// <summary>
  ///   Accepts a function that can alter the message stream.
  ///   Ideally suited for use with Reactive Extensions.
  ///   <code>
  ///     open FSharp.Control.Reactive
  ///     let delay dispatch =
  ///       let subject = Subject.broadcast
  ///       let observable = subject :&gt; System.IObservable&lt;_&gt;
  ///       observable
  ///       |&gt; Observable.delay (System.TimeSpan.FromSeconds 1.0)
  ///       |&gt; Observable.subscribe dispatch
  ///       |&gt; ignore
  ///       subject.OnNext
  ///
  ///     // ...
  ///
  ///     binding |&gt; Binding.alterMsgStream delay
  ///   </code>
  /// </summary>
  /// <param name="alteration">The function that will alter the message stream.</param>
  /// <param name="binding">The binding to which the message stream is altered.</param>
  let alterMsgStream (alteration: ('b -> unit) -> 'a -> unit) (binding: BindingT<'model, 'a, 'vm>) : BindingT<'model, 'b, 'vm> =
    binding
    |> (alterMsgStream alteration |> mapData)

  module OneWay =
    open BindingData.OneWay

    let opt =
      create id
      |> createStatic


  module OneWayToSource =
    open BindingData.OneWayToSource
    
    let id =
      create (fun obj _ -> obj)
      |> createStatic

  module Cmd =
    open BindingData.Cmd

    let createWithParam exec canExec autoRequery =
      createWithParam exec canExec autoRequery
      |> createStatic

  module SubModel =
    open BindingData.SubModel

    let opt createVm : StaticBindingT<'bindingModel voption, 'msg, 'viewModel :> ISubModel<'bindingModel, 'msg>> =
      create id createVm (fun ((vm: #ISubModel<'bindingModel,'msg>),m) -> vm.StaticHelper.UpdateModel(m)) (fun _ m -> m)
      |> createStatic


[<AllowNullLiteral>]
type InnerExampleViewModel(args) as this =
  interface ISubModel<String, Int64> with
    member _.StaticHelper = StaticHelper.create args (fun () -> this)
    
[<AllowNullLiteral>]
type ExampleViewModel(args) as this =
  let staticHelper = StaticHelper.create args (fun () -> this)

  member _.Model
    with get() = BindingT.OneWay.opt |> staticHelper.GetValue
    and set(v) = BindingT.OneWayToSource.id >> BindingT.mapMsg int32<string> |> staticHelper.SetValue(v)
  member _.Command = BindingT.Cmd.createWithParam (fun _ _ -> ValueNone) (fun _ _ -> true) false |> staticHelper.GetValue
  member _.SubModel = BindingT.SubModel.opt InnerExampleViewModel >> BindingT.mapModel ValueSome >> BindingT.mapMsg int32 |> staticHelper.GetValue
