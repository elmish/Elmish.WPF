module Elmish.WPF.ViewModel

open System.ComponentModel
open System.Dynamic
open System.Windows
open System.Collections.ObjectModel

type PropertyAccessor<'model,'msg> =
    | Get of Getter<'model>
    | GetSet of Getter<'model> * Setter<'model,'msg>
    | Cmd of Command
//    | ObsCol of Getter<'model>
    | ObsCol of ('model -> ObservableCollection<int>)
    | Vm of ViewModelBase<'model,'msg>

and ViewModelBase<'model, 'msg>(m:'model, dispatch, propMap: ViewBindings<'model,'msg>) as this =
    inherit DynamicObject()

    let propertyChanged = Event<PropertyChangedEventHandler,PropertyChangedEventArgs>()
    let notifyPropertyChanged name = 
        propertyChanged.Trigger(this,PropertyChangedEventArgs(name))

    let mutable model : 'model = m

    let props = new System.Collections.Generic.Dictionary<string, PropertyAccessor<'model,'msg>>()

    let notify (p:string list) =
        p |> List.iter notifyPropertyChanged
        let raiseCanExecuteChanged =
            function
            | Cmd c -> 
                fun _ -> c.RaiseCanExecuteChanged()
                |> Application.Current.Dispatcher.Invoke
            | _ -> ()
        //TODO on raise for cmds that depend on props in p
        props |> List.ofSeq |> List.iter (fun kvp -> raiseCanExecuteChanged kvp.Value)

    let buildProps =
        let toCommand (exec, canExec) = Command((fun () -> exec model |> dispatch), fun () -> canExec model)
        let toObsCol (getter: 'model -> seq<obj>) = 
            let o = ObservableCollection<int>()
//            getter model |> unbox |> Seq.iter (fun p -> o.Add <| unbox p)
            fun (model:'model) -> 
                o.Clear()
                let s = getter model
                getter model |> Seq.iter (fun p -> p :?> int |> o.Add)
                o
        
//        let toObsCol =
//            let o = ObservableCollection()
//            fun (model:'model) -> 
//                o.Clear()
//                let s = getter model
//                getter model |> unbox |> Seq.iter (fun p -> o.Add <| unbox p)
//                o

                

        let toSubView propMap = ViewModelBase<'model,'msg>(model, dispatch, propMap)
        let rec convert = 
            List.map (fun (name,binding) ->
                match binding with
                | Bind getter -> name, Get getter
                | BindTwoWay (getter,setter) -> name, GetSet (getter,setter)
                | BindCmd exec -> name, Cmd <| toCommand (exec,(fun _ -> true))
                | BindCmdIf (exec,canExec) -> name, Cmd <| toCommand (exec,canExec)
                | BindVm (getter, propMap) -> name, Vm <| toSubView propMap
            )
        
        convert propMap |> List.iter (fun (n,a) -> props.Add(n,a))

    do buildProps

    interface INotifyPropertyChanged with
        [<CLIEvent>]
        member this.PropertyChanged = propertyChanged.Publish

    member this.UpdateModel other =
        let propDiff name =
            function
            | Get getter | GetSet (getter,_) ->
                if getter model <> getter other then Some name else None
            | Vm vm ->
                vm.UpdateModel other
                None
            | _ -> None

        let diffs = 
            props
            |> Seq.toList
            |> List.map (fun (kvp) -> propDiff kvp.Key kvp.Value)
            |> List.choose id
        
        model <- other
        notify diffs


    // DynamicObject overrides

    override this.TryGetMember (binder, r) = 
        printfn "TryGetMember %s = %b" binder.Name (props.ContainsKey binder.Name)
        if props.ContainsKey binder.Name then
            r <-
                match props.[binder.Name] with 
                | Get getter 
                | GetSet (getter,_) -> getter model
                | Cmd c -> unbox c
                | ObsCol getter -> getter model |> unbox
                | Vm m -> unbox m
            true
        else false

    override this.TrySetMember (binder, value) =
        if props.ContainsKey binder.Name then
            match props.[binder.Name] with 
            | GetSet (_,setter) -> setter value model |> dispatch
            | _ -> invalidOp "Unable to set read-only member"
            true
        else false