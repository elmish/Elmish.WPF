namespace Elmish.WPF

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
      let! vmBinding =
        option {
          match setBindings.TryGetValue name with
          | true, value ->
            return value |> MapOutputType.unboxVm
          | _ ->
            let binding = binding name
            let! vmBinding =
              Initialize(args.loggingArgs, name, getFunctionsForSubModelSelectedItem)
                .Recursive(currentModel, dispatch, (fun () -> currentModel), binding.DataT)
            do getBindings.Add (name, vmBinding |> MapOutputType.boxVm)
            return vmBinding
          }
      return!
        match Get(nameChain).Recursive(currentModel, vmBinding) with
        | Ok x -> Some x
        | Error _ -> None
    } |> Option.defaultValue null

  member _.SetValue (value, [<CallerMemberName>] ?memberName: string) =
    fun (binding: StaticBindingT<'model, 'msg, 'a>) ->
      option {
        let! name = memberName
        let! vmBinding =
          option {
            match setBindings.TryGetValue name with
            | true, value ->
              return value |> MapOutputType.unboxVm
            | _ ->
              let binding = binding name
              let! vmBinding =
                Initialize(args.loggingArgs, name, getFunctionsForSubModelSelectedItem)
                  .Recursive(currentModel, dispatch, (fun () -> currentModel), binding.DataT)
              do setBindings.Add (name, vmBinding |> MapOutputType.boxVm)
              return vmBinding
            }
        return
          Set(value).Recursive(currentModel, vmBinding) |> ignore
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

module StaticHelper =
  let create args getSender = StaticHelper(args, getSender)

[<AllowNullLiteral>]
type ISubModel<'model, 'msg> =
  abstract StaticHelper: StaticHelper<'model, 'msg>
