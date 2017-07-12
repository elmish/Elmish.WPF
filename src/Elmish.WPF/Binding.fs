namespace Elmish.WPF
  
open System
open System.Windows.Input
open System.Windows.Data

type Getter<'model> = 'model -> obj
type Setter<'model,'msg> = obj -> 'model -> 'msg
type Execute<'model,'msg> = obj -> 'model -> 'msg
type CanExecute<'model> = obj -> 'model -> bool

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
    | BindCmd of Execute<'model,'msg> * CanExecute<'model>
    | BindModel of Getter<'model> * ViewBindings<'model,'msg>
    | BindMap of Getter<'model> * (obj -> obj)

[<RequireQualifiedAccess>]
module Binding =
    
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
            | BindCmd (exec,canExec) ->
                ((fun v m -> (toModel m) |> exec v |> toMsg), (fun v m -> (toModel m) |> canExec v))
                |> BindCmd
            | BindModel (getter,binding) ->
                (toModel >> getter, binding |> mapViewBinding toModel toMsg)
                |> BindModel
            | BindMap (getter,mapper) ->
                ((toModel >> getter), mapper)
                |> BindMap

        viewBinding
        |> List.map (fun (n,v) -> n, mapVariable v)

    // Helper functions that clean up binding creation

    let oneWay (getter: 'model -> 'a) p : ViewBinding<'model,'msg> = 
        p, Bind (getter >> unbox)
    let twoWay (getter: 'model -> 'a) (setter: 'a -> 'model -> 'msg) p : ViewBinding<'model,'msg> = 
        p, BindTwoWay (getter >> unbox, fun v m -> setter (v :?> 'a) m)
    let cmd exec p : ViewBinding<'model,'msg> = 
        p, BindCmd (exec, fun _ _ -> true)
    let cmdIf exec canExec p : ViewBinding<'model,'msg> = 
        p, BindCmd (exec, canExec)
    let model (getter: 'model -> '_model) (viewBinding: ViewBindings<'_model,'_msg>) (toMsg: '_msg -> 'msg) p : ViewBinding<'model,'msg> = 
        p, BindModel (getter >> unbox, viewBinding |> mapViewBinding getter toMsg)
    let oneWayMap (getter: 'model -> 'a) (mapper: 'a -> 'b) p : ViewBinding<'model,'msg> =
        p, BindMap (getter >> unbox, unbox >> mapper >> unbox)