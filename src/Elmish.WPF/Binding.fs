namespace Elmish.WPF
  
open System
open System.Windows.Input

type Getter<'model> = 'model -> obj
type Setter<'model,'msg> = obj -> 'model -> 'msg
type Execute<'model,'msg> = 'model -> 'msg
type CanExecute<'model> = 'model -> bool

type Command(execute, canExecute) =
    let canExecuteChanged = Event<EventHandler,EventArgs>()
    member x.RaiseCanExecuteChanged _ = canExecuteChanged.Trigger(x,EventArgs.Empty)
    interface ICommand with
        [<CLIEvent>]
        member x.CanExecuteChanged = canExecuteChanged.Publish
        member x.CanExecute _ = canExecute()
        member x.Execute _ = execute()

type ViewBinding<'model,'msg> = string * Variable<'model,'msg>
and ViewBindings<'model,'msg> = ViewBinding<'model,'msg> list
and Variable<'model,'msg> =
    | Bind of Getter<'model>
    | BindTwoWay of Getter<'model> * Setter<'model,'msg>
    | BindCmd of Execute<'model,'msg>
    | BindCmdIf of Execute<'model,'msg> * CanExecute<'model>
    | BindVm of Getter<'model> * ViewBindings<'model,'msg>
    | BindMap of Getter<'model> * (obj -> obj)

[<RequireQualifiedAccess>]
module Binding =
    
    /// Maps a set of view bindings to its parent view bindings
    let rec private mapViewBinding<'model,'msg,'_model,'_msg> mModel mMsg (viewBinding: ViewBindings<'_model,'_msg>) : ViewBindings<'model,'msg> =
        let mapVariable =
            function
            | Bind getter ->                
                mModel >> getter 
                |> Bind
            | BindTwoWay (getter,setter) -> 
                (mModel >> getter, fun v m -> (mModel m) |> (setter v >> mMsg))
                |> BindTwoWay
            | BindCmd exec ->
                mModel >> exec >> mMsg
                |> BindCmd
            | BindCmdIf (exec,canExec) ->
                (mModel >> exec >> mMsg, mModel >> canExec)
                |> BindCmdIf
            | BindVm (getter,binding) ->
                (mModel >> getter, binding |> mapViewBinding mModel mMsg)
                |> BindVm
            | BindMap (getter,mapper) ->
                ((mModel >> getter), mapper)
                |> BindMap

        viewBinding
        |> List.map (fun (n,v) -> n, mapVariable v)
            


    // Helper functions that clean up binding creation

    let oneWay (getter: 'model -> 'a) p : ViewBinding<'model,'msg> = 
        p, Bind (getter >> unbox)
    let twoWay (getter: 'model -> 'a) (setter: 'a -> 'model -> 'msg) p : ViewBinding<'model,'msg> = 
        p, BindTwoWay ((getter >> unbox), (fun v m -> setter (v :?> 'a) m))
    let cmd exec p : ViewBinding<'model,'msg> = 
        p, BindCmd exec
    let cmdIf exec canExec p : ViewBinding<'model,'msg> = 
        p, BindCmdIf (exec, canExec)
    let vm (getter: 'model -> '_model) (viewBinding: ViewBindings<'_model,'_msg>) (toMsg: '_msg -> 'msg) p : ViewBinding<'model,'msg> = 
        p, BindVm ((getter >> unbox), viewBinding |> mapViewBinding getter toMsg)
    let oneWayMap (getter: 'model -> 'a) (mapper: 'a -> 'b) p : ViewBinding<'model,'msg> =
        p, BindMap (getter >> unbox, unbox >> mapper >> unbox)
            