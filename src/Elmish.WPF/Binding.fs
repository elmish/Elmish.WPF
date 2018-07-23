namespace Elmish.WPF
  
open System
open System.Windows.Input
open System.Collections.Generic
open System.Linq

type IndexOrKey =
    | Index of int
    | Key of string

type Getter<'model> = 'model -> obj
type Setter<'model,'msg> = obj -> 'model -> 'msg
type ValidSetter<'model,'msg> = obj -> 'model -> Result<'msg,string>
type Execute<'model,'msg> = obj -> 'model -> 'msg
type CanExecute<'model> = obj -> 'model -> bool
type KeyedGetter<'model> = 'model -> IndexOrKey -> obj
type KeyedSetter<'model,'msg> = obj -> 'model -> IndexOrKey -> 'msg

type Command(execute, canExecute) as this =
    let canExecuteChanged = Event<EventHandler,EventArgs>()
    let handler = EventHandler(fun _ _ -> this.RaiseCanExecuteChanged()) 
    do CommandManager.RequerySuggested.AddHandler(handler)
    // CommandManager only keeps a weak reference to the event handler, so a strong handler must be maintained
    member private x._Handler = handler
    member x.RaiseCanExecuteChanged () = canExecuteChanged.Trigger(x,EventArgs.Empty)
    interface ICommand with
        [<CLIEvent>]
        member x.CanExecuteChanged = canExecuteChanged.Publish
        member x.CanExecute p = canExecute p
        member x.Execute p = execute p

type ViewBinding<'model,'msg> = string * Variable<'model,'msg>
and ViewBindings<'model,'msg> = ViewBinding<'model,'msg> list
and Variable<'model,'msg> =
    | Bind of Getter<'model>    
    | BindTwoWay of Getter<'model> * Setter<'model,'msg>
    | BindTwoWayValidation of Getter<'model> * ValidSetter<'model,'msg>
    | BindCmd of Execute<'model,'msg> * CanExecute<'model>
    | BindModel of Getter<'model> * ViewBindings<'model,'msg>
    | BindMap of Getter<'model> * (obj -> obj)
    | BindCollectionTwoWay of Getter<'model> * KeyedGetter<'model> * KeyedSetter<'model,'msg>
    | BindCollectionModel of ('model -> IEnumerable<obj>) * (int -> ViewBindings<'model,'msg>)
    

[<RequireQualifiedAccess>]
module Binding =
    open System.Collections
    open System.Collections.ObjectModel
    
    // Maps a set of view bindings to its parent view bindings
    let rec private mapViewBinding<'model,'msg,'_model,'_msg> toModel toMsg (viewBinding: ViewBindings<'_model,'_msg>) : ViewBindings<'model,'msg> =
        let mapVariable =
            function
            | Bind getter ->
                toModel >> getter
                |> Bind
            | BindTwoWay (getter,setter) -> 
                (toModel >> getter, fun v m -> (toModel m) |> (setter v >> toMsg))
                |> BindTwoWay
            | BindTwoWayValidation (getter,setter) -> 
                (toModel >> getter, fun v m -> (toModel m) |> (setter v >> Result.map toMsg))
                |> BindTwoWayValidation
            | BindCmd (exec,canExec) ->
                ((fun v m -> (toModel m) |> exec v |> toMsg), (fun v m -> (toModel m) |> canExec v))
                |> BindCmd
            | BindModel (getter,binding) ->
                (toModel >> getter, binding |> mapViewBinding toModel toMsg)
                |> BindModel
            | BindMap (getter,mapper) ->
                ((toModel >> getter), mapper)
                |> BindMap
            | BindCollectionTwoWay (listGetter, keyedGetter, keyedSetter) ->
                ((toModel >> listGetter), (toModel >> keyedGetter), (fun v m k -> keyedSetter v (toModel m) k |> toMsg))
                |> BindCollectionTwoWay 
            | BindCollectionModel (listGetter, bindings) ->
                ((toModel >> listGetter), fun i -> bindings i |> mapViewBinding toModel toMsg)
                |> BindCollectionModel

        viewBinding
        |> List.map (fun (n,v) -> n, mapVariable v)

    // Helper functions that clean up binding creation

    ///<summary>Source to target binding (i.e. BindingMode.OneWay)</summary>
    ///<param name="getter">Gets value from the model</param>
    ///<param name="name">Binding name</param>
    let oneWay (getter: 'model -> 'a) name : ViewBinding<'model,'msg> = 
        name, Bind (getter >> unbox)
    
    ///<summary>Either source to target or target to source (i.e. BindingMode.TwoWay)</summary>
    ///<param name="getter">Gets value from the model</param>
    ///<param name="setter">Setter function, returns a message to dispatch, typically to set the value in the model</param>
    ///<param name="name">Binding name</param>
    let twoWay (getter: 'model -> 'a) (setter: 'a -> 'model -> 'msg) name : ViewBinding<'model,'msg> = 
        name, BindTwoWay (getter >> unbox, fun v m -> setter (v :?> 'a) m)
    
    ///<summary>Either source to target or target to source (i.e. BindingMode.TwoWay) with INotifyDataErrorInfo implementation)</summary>
    ///<param name="getter">Gets value from the model</param>
    ///<param name="setter">Validation function, returns a Result with the message to dispatch or an error string</param>
    ///<param name="name">Binding name</param>
    let twoWayValidation (getter: 'model -> 'a) (setter: 'a -> 'model -> Result<'msg,string>) name : ViewBinding<'model,'msg> = 
        name, BindTwoWayValidation (getter >> unbox, fun v m -> setter (v :?> 'a) m)
        
    ///<summary>Command binding</summary>
    ///<param name="exec">Execute function, returns a message to dispatch</param>
    ///<param name="name">Binding name</param>
    let cmd exec name : ViewBinding<'model,'msg> = 
        name, BindCmd (exec, fun _ _ -> true)
        
    ///<summary>Conditional command binding</summary>
    ///<param name="exec">Execute function, returns a message to dispatch</param>
    ///<param name="canExec">CanExecute function, returns a bool</param>
    ///<param name="name">Binding name</param>
    let cmdIf exec canExec name : ViewBinding<'model,'msg> = 
        name, BindCmd (exec, canExec)
        
    ///<summary>Sub-view binding</summary>
    ///<param name="getter">Gets the sub-model from the base model</param>
    ///<param name="viewBinding">Set of view bindings for the sub-view</param>
    ///<param name="toMsg">Maps sub-messages to the base message type</param>
    ///<param name="name">Binding name</param>
    let model (getter: 'model -> '_model) (viewBinding: ViewBindings<'_model,'_msg>) (toMsg: '_msg -> 'msg) name : ViewBinding<'model,'msg> = 
        name, BindModel (getter >> unbox, viewBinding |> mapViewBinding getter toMsg)
        
    ///<summary>One-way binding that applies a map when passing data to the view.
    /// Should be used for data that a view needs wrapped in some view-specific type. 
    /// For example when graphing a series, the data can be stored as a plain array in the model, 
    /// and then mapped to a SeriesCollection for the view.</summary>
    ///<param name="getter">Gets the value from the model</param>
    ///<param name="mapper">Maps the value for consumption by the view</param>
    ///<param name="name">Binding name</param>
    let oneWayMap (getter: 'model -> 'a) (mapper: 'a -> 'b) name : ViewBinding<'model,'msg> =
        name, BindMap (getter >> unbox, unbox >> mapper >> unbox)

    ///<summary>Either source to target or target to source when the source is a list (i.e. BindingMode.TwoWay)</summary>
    ///<param name="getList">Gets list from the model</param>
    ///<param name="getItem">Gets an item from the list from the model with a given index</param>
    ///<param name="getList">Setter function, returns a message to dispatch, to set the value of an indexed item</param>
    ///<param name="name">Binding name</param>
    let collectionTwoWay (listGetter:'model -> seq<'a>) (keyedGetter:'model -> IndexOrKey -> 'a) (keyedSetter:'a -> 'model -> IndexOrKey -> 'msg) name : ViewBinding<'model,'msg> = 
        let unwrapGetter getter = fun m k -> getter m k |> unbox
        let unwrapSetter setter = fun v m -> setter (unbox v) m

        name, BindCollectionTwoWay (listGetter >> unbox, (unwrapGetter keyedGetter), unwrapSetter keyedSetter)


    ///<summary>Sub-view binding</summary>
    ///<param name="getter">Gets the sub-model from the base model</param>
    ///<param name="viewBinding">Set of view bindings for the sub-view</param>
    ///<param name="toMsg">Maps sub-messages to the base message type</param>
    ///<param name="name">Binding name</param>
    let collectionModel (getter:'model -> IEnumerable<'_model>) (viewBindings: unit -> ViewBindings<'_model,'_msg>)  (toMsg: int -> '_msg -> 'msg) name : ViewBinding<'model,'msg> = 
        
        let getter' m =
            let result = getter m
            match result with
            | :? ObservableCollection<'_model> as obsc -> (ObservableCollection.map (fun x -> box x) obsc) :> IEnumerable<obj>
            | ienumerable -> ienumerable |> Seq.map box
            
        let getter'' m i =
            let result = getter m
            match result with
            | :? ObservableCollection<'_model> as obsc -> obsc.[i]
            | ienumerable -> ienumerable |> Seq.item i

        let unwrapViewBindings i =
            viewBindings() |> mapViewBinding (fun m -> getter'' m i (*(getter m) |> Seq.item i*)) (toMsg i)

        name, BindCollectionModel(getter' >> unbox,  unwrapViewBindings)