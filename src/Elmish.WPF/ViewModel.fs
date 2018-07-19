module Elmish.WPF.ViewModel

open System
open System.ComponentModel
open System.Dynamic
open System.Windows
open System.Collections
open System.Collections.Generic

open Elmish.WPF.CollectionViewModel      
open System.Collections.ObjectModel
open System.Windows.Data

type PropertyAccessor<'model,'msg> =
    | Get of Getter<'model>    
    | GetSet of Getter<'model> * Setter<'model,'msg>
    | GetSetValidate of Getter<'model> * ValidSetter<'model,'msg>
    | Cmd of Command
    | Model of ViewModelBase<'model,'msg>
    | Map of Getter<'model> * (obj -> obj)
    | CollectionGetSet of Getter<'model> * KeyedGetter<'model> * KeyedSetter<'model,'msg>
    | CollectionModel of KeyedGetter<'model> (*'model -> string -> obj*) * seq<ViewModelBase<'model,'msg>>
                                                                

and ViewModelBase<'model, 'msg>(m:'model, dispatch, propMap: ViewBindings<'model,'msg>, debug:bool) as this =
    inherit DynamicObject()

    let log msg = if debug then console.log msg
    
    // Store all bound properties and their corresponding accessors
    let props = new System.Collections.Generic.Dictionary<string, PropertyAccessor<'model,'msg>>()
    // Store all errors
    let errors = new System.Collections.Generic.Dictionary<string, string list>()

    // Store two-way binders so they are not GC'd  
    let twoWays = System.Collections.Generic.List<ListViewModel<'model,'msg>>()
    //let models = System.Collections.Generic.Dictionary<string,ModelViewModel<'model,'msg>>()
    let models = System.Collections.Generic.Dictionary<string,ObservableCollection<_>>()
    
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
                | BindCollectionTwoWay (mgetter, kgetter, ksetter) -> name, CollectionGetSet (mgetter, kgetter, ksetter) 
                | BindCollectionModel (mgetter, binding) -> 
                    let kgetter m = function
                        | Index i -> (mgetter m) |> Seq.item i |> box
                        | Key _ -> failwith "?"
                        
                    name, CollectionModel ( kgetter,
                                            mgetter model |> Seq.mapi (fun i m -> toSubView <| binding (Index i)))
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
            | CollectionGetSet (listGetter, _, _) ->
                if listGetter model <> listGetter other then Some name else None
            | CollectionModel (kgetter, viewmodels) ->       
                viewmodels 
                |> Seq.mapi (fun i m -> i,m)
                |> Seq.filter (fun (i,m) -> kgetter model (Index i) <> kgetter other (Index i))
                |> Seq.iter (fun (i,m) -> (m.UpdateModel other))
                Some name                
            | _ -> None
        let diffs = 
            props
            |> Seq.choose (fun (kvp) -> propDiff kvp.Key kvp.Value)
            |> Seq.toList
        
        model <- other
        
        // clear 2-way binders list
        twoWays.Clear() |> ignore
        
        notify (diffs)
   
    member __.UpdateSubModels (m: obj) = ()

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
                | CollectionGetSet (listGetter, keyedGetter, keyedSetter) -> 
                    let binder = ListViewModel(binder.Name, 
                                               unbox<seq<obj>> (listGetter model),
                                               keyedGetter model ,
                                               (fun v k -> keyedSetter v model k |> dispatch))
                    twoWays.Add binder
                    box binder
                | CollectionModel (_,viewmodels) ->
                    if models.ContainsKey binder.Name then
                        box models.[binder.Name]
                    else                       
                        let obsc = new ObservableCollection<_>(viewmodels)
                        for b in obsc do
                            (b :> INotifyPropertyChanged).PropertyChanged.Add (fun arg -> printfn "%s" arg.PropertyName) // <- never raised - why ?
                        obsc.CollectionChanged.Add (fun arg -> printfn "%A" arg )
                        models.Add (binder.Name, obsc)
                        box obsc
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