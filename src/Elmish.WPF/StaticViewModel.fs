module Elmish.WPF.StaticViewModel

open System
open System.Collections.Generic
open System.ComponentModel
open System.Runtime.CompilerServices
open Microsoft.Extensions.Logging

open Elmish.WPF.BindingVmHelpers



type internal StaticHelper<'model, 'msg>(args: ViewModelArgs<'model, 'msg>, getSender: unit -> obj) =

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

  member _.GetValue (binding: BindingData<'model, 'msg, 'a>, [<CallerMemberName>] ?memberName: string) =
    option {
      let! name = memberName

      let! b =
        option {
          if getBindings.ContainsKey name then
            return getBindings.Item name |> MapOutputType.recursivecase unbox box
          else
            //let binding = BindingData.mapVm box unbox binding
            let! b =
              Initialize(args.loggingArgs, name, getFunctionsForSubModelSelectedItem)
                .Recursive(currentModel, dispatch, (fun () -> currentModel), binding)
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
    fun (binding: BindingData<'model, 'msg, 'a>) ->
      option {
        let! name = memberName

        let! b =
          option {
            if setBindings.ContainsKey name then
              return setBindings.Item name |> MapOutputType.recursivecase unbox box
            else
              //let binding = BindingData.mapVm box unbox binding
              let! b =
                Initialize(args.loggingArgs, name, getFunctionsForSubModelSelectedItem)
                  .Recursive(currentModel, dispatch, (fun () -> currentModel), binding)
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

type ExampleViewModel() as this =
  let staticHelper = StaticHelper<String, Int32>(ViewModelArgs.simple "", fun () -> this)

  member _.Model = BindingData.OneWay.create id |> staticHelper.GetValue
  member _.Command = BindingData.Cmd.createWithParam (fun _ _ -> ValueNone) (fun _ _ -> true) false |> staticHelper.GetValue
  member _.SubModel = BindingData.SubModel.create (fun x -> x |> ValueSome) (failwith "x") (failwith "y") (fun _x y -> y) |> staticHelper.GetValue
