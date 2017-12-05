module Elmish.WPF.ViewModel

open System.ComponentModel
open System.Dynamic
open System.Windows

type PropertyAccessor<'model,'msg> =
    | Get of Getter<'model>
    | GetSet of Getter<'model> * Setter<'model,'msg>
    | GetSetValidate of Getter<'model> * ValidSetter<'model,'msg>
    | Cmd of Command
    | Model of ViewModelBase<'model,'msg>
    | Map of Getter<'model> * (obj -> obj)

and ViewModelBase<'model, 'msg>(m:'model, dispatch, propMap: ViewBindings<'model,'msg>, debug:bool) as this =
    inherit DynamicObject()

    let log msg = if debug then console.log msg
    
    // Store all bound properties and their corresponding accessors
    let props = new System.Collections.Generic.Dictionary<string, PropertyAccessor<'model,'msg>>()
    // Store all errors
    let errors = new System.Collections.Generic.Dictionary<string, string list>()

    // Current model
    let mutable model : 'model = m

    // For INotifyPropertyChanged

    let propertyChanged = Event<PropertyChangedEventHandler,PropertyChangedEventArgs>()
    let notifyPropertyChanged name = 
        log <| sprintf "Notify %s" name
        propertyChanged.Trigger(this,PropertyChangedEventArgs(name))
    let notify (p:string list) =
        p |> List.iter notifyPropertyChanged
        let raiseCanExecuteChanged =
            function
            | Cmd c ->
                if isNull Application.Current then ()
                elif isNull Application.Current.Dispatcher then () else
                fun _ -> c.RaiseCanExecuteChanged()
                |> Application.Current.Dispatcher.Invoke
            | _ -> ()
        //TODO only raise for cmds that depend on props in p
        props |> List.ofSeq |> List.iter (fun kvp -> raiseCanExecuteChanged kvp.Value)
    

    // For INotifyDataErrorInfo

    let errorsChanged = new DelegateEvent<System.EventHandler<DataErrorsChangedEventArgs>>()


    // Initialize bindings
    do
        let toCommand (exec, canExec) = Command((fun p -> exec p model |> dispatch), fun p -> canExec p model)
        let toSubView propMap = ViewModelBase<_,_>(model, dispatch, propMap, debug)
        let rec convert = 
            List.map (fun (name,binding) ->
                match binding with
                | Bind getter -> name, Get getter
                | BindTwoWay (getter,setter) -> name, GetSet (getter,setter)
                | BindTwoWayValidation (getter,setter) -> name, GetSetValidate (getter,setter)
                | BindCmd (exec,canExec) -> name, Cmd <| toCommand (exec,canExec)
                | BindModel (_, propMap) -> name, Model <| toSubView propMap
                | BindMap (getter,mapper) -> name, Map <| (getter,mapper)
            )
        
        convert propMap |> List.iter (fun (n,a) -> props.Add(n,a))

    interface INotifyPropertyChanged with
        [<CLIEvent>]
        member __.PropertyChanged = propertyChanged.Publish
        
    interface INotifyDataErrorInfo with
        [<CLIEvent>]
        member __.ErrorsChanged = errorsChanged.Publish
        member __.HasErrors = errors.Count > 0
        member __.GetErrors propName = 
            log <| sprintf "Getting errors for %s" propName
            match errors.TryGetValue propName with
            | true, errs -> errs
            | false, _ -> []
            :> System.Collections.IEnumerable
    
    member __.UpdateModel other =
        log <| sprintf "UpdateModel %A" (props.Keys |> Seq.toArray)
        let propDiff name =
            function
            | Get getter | GetSet (getter,_) | GetSetValidate(getter,_) | Map (getter,_) ->
                if getter model <> getter other then Some name else None
            | Model m ->
                m.UpdateModel other
                None
            | _ -> None

        let diffs = 
            props
            |> Seq.choose (fun (kvp) -> propDiff kvp.Key kvp.Value)
            |> Seq.toList
        
        model <- other
        notify diffs


    // DynamicObject overrides

    override __.TryGetMember (binder, r) = 
        log <| sprintf "TryGetMember %s" binder.Name
        if props.ContainsKey binder.Name then
            r <-
                match props.[binder.Name] with 
                | Get getter 
                | GetSet (getter,_)
                | GetSetValidate (getter,_) -> getter model
                | Cmd c -> unbox c
                | Model m -> unbox m
                | Map (getter,mapper) -> getter model |> mapper
            true
        else false

    override __.TrySetMember (binder, value) =
        log <| sprintf "TrySetMember %s" binder.Name
        if props.ContainsKey binder.Name then
            match props.[binder.Name] with 
            | GetSet (_,setter) -> try setter value model |> dispatch with | _ -> ()
            | GetSetValidate (_,setter) -> 
                let errorsChanged() = errorsChanged.Trigger([| unbox this; unbox <| DataErrorsChangedEventArgs(binder.Name) |])
                try 
                    match setter value model with
                    | Ok msg -> 
                        if errors.Remove(binder.Name) then errorsChanged()
                        dispatch msg 
                    | Error err ->
                        match errors.TryGetValue binder.Name with
                        | true, errs -> errors.[binder.Name] <- err :: errs
                        | false, _ -> errors.Add(binder.Name, [err])
                        errorsChanged()
                with | _ -> ()
            | _ -> invalidOp "Unable to set read-only member"
        false