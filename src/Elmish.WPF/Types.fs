namespace Elmish.WPF.Internal

open System
open System.Windows.Input


/// Represents all necessary data used to create the different binding types.
type BindingSpecData<'model, 'msg> =
  | OneWaySpec of get: ('model -> obj)
  | OneWayLazySpec of
      get: ('model -> obj)
      * map: (obj -> obj)
      * equals: (obj -> obj -> bool)
  | OneWaySeqSpec of
      get: ('model -> obj seq)
      * getId: (obj -> obj)
      * equals: (obj -> obj -> bool)
  | TwoWaySpec of get: ('model -> obj) * set: (obj -> 'model -> 'msg)
  | TwoWayValidateSpec of
      get: ('model -> obj)
      * set: (obj -> 'model -> 'msg)
      * validate: ('model -> Result<obj, string>)
  | TwoWayIfValidSpec of
      get: ('model -> obj)
      * set: (obj -> 'model -> Result<'msg, string>)
  | CmdSpec of
      exec: ('model -> 'msg)
      * canExec: ('model -> bool)
  | CmdIfValidSpec of exec: ('model -> Result<'msg, obj>)
  | ParamCmdSpec of
      exec: (obj -> 'model -> 'msg)
      * canExec: (obj -> 'model -> bool)
      * autoRequery: bool
  | SubModelSpec of
      getModel: ('model -> obj option)
      * getBindings: (unit -> BindingSpec<obj, obj> list)
      * toMsg: (obj -> 'msg)
  | SubModelSeqSpec of
      getModels: ('model -> obj seq)
      * getId: (obj -> obj)
      * getBindings: (unit -> BindingSpec<obj, obj> list)
      * toMsg: (obj * obj -> 'msg)

/// Represents all necessary data used to create a binding.
and BindingSpec<'model, 'msg> =
  { Name: string
    Data: BindingSpecData<'model, 'msg> }


module BindingSpecData =

  let box : BindingSpecData<'model, 'msg> -> BindingSpecData<obj, obj> = function
  | OneWaySpec get -> OneWaySpec (unbox >> get)
  | OneWayLazySpec (get, map, equals) -> OneWayLazySpec (unbox >> get, map, equals)
  | OneWaySeqSpec (get, getId, equals) -> OneWaySeqSpec (unbox >> get, getId, equals)
  | TwoWaySpec (get, set) ->
      TwoWaySpec (unbox >> get, (fun v m -> set v (unbox m) |> box))
  | TwoWayValidateSpec (get, set, validate) ->
      let boxedSet v m = set v (unbox m) |> box
      TwoWayValidateSpec (unbox >> get, boxedSet, unbox >> validate)
  | TwoWayIfValidSpec (get, set) ->
      let boxedSet v m = set v (unbox m) |> Result.map box
      TwoWayIfValidSpec (unbox >> get, boxedSet)
  | CmdSpec (exec, canExec) -> CmdSpec (unbox >> exec >> box, unbox >> canExec)
  | CmdIfValidSpec exec -> CmdIfValidSpec (unbox >> exec >> Result.map box)
  | ParamCmdSpec (exec, canExec, autoRequery) ->
      let boxedExec p m = exec p (unbox m) |> box
      let boxedCanExec p m = canExec p (unbox m)
      ParamCmdSpec (boxedExec, boxedCanExec, autoRequery)
  | SubModelSpec (getModel, getBindings, toMsg) ->
      SubModelSpec (unbox >> getModel, getBindings, toMsg >> unbox)
  | SubModelSeqSpec (getModel, isSame, getBindings, toMsg) ->
      SubModelSeqSpec (unbox >> getModel, isSame, getBindings, toMsg >> unbox)


/// A command that optionally hooks into CommandManager.RequerySuggested to
/// automatically trigger CanExecuteChanged whenever the CommandManager detects
/// conditions that might change the output of canExecute. It's necessary to use
/// this feature for command bindings where the CommandParameter is bound to
/// another UI control (e.g. a ListView.SelectedItem).
type Command(execute, canExecute, autoRequery) as this =

  let canExecuteChanged = Event<EventHandler,EventArgs>()
  let handler = EventHandler(fun _ _ -> this.RaiseCanExecuteChanged())

  do if autoRequery then CommandManager.RequerySuggested.AddHandler(handler)

  // CommandManager only keeps a weak reference to the event handler, so a
  // strong handler must be maintained
  member private x._Handler = handler

  member x.RaiseCanExecuteChanged () = canExecuteChanged.Trigger(x,EventArgs.Empty)

  interface ICommand with
    [<CLIEvent>]
    member x.CanExecuteChanged = canExecuteChanged.Publish
    member x.CanExecute p = canExecute p
    member x.Execute p = execute p
