[<AutoOpen>]
module internal Elmish.WPF.InternalTypes

open System
open System.Windows.Input

module Option =
  let apply a = Option.map (fun f -> f a)

/// A command that optionally hooks into CommandManager.RequerySuggested to
/// automatically trigger CanExecuteChanged whenever the CommandManager detects
/// conditions that might change the output of canExecute. It's necessary to use
/// this feature for command bindings where the CommandParameter is bound to
/// another UI control (e.g. a ListView.SelectedItem).
type Command(execute: obj -> unit, canExecute: obj -> bool, autoRequery) as this =

  let mutable _execute = Some execute

  let mutable _canExecute = Some canExecute

  let mutable _canExecuteChanged = new Event<EventHandler,EventArgs>()

  let mutable _handler = EventHandler(fun _ _ -> this.RaiseCanExecuteChanged())

  do if autoRequery then CommandManager.RequerySuggested.AddHandler(_handler)

  // CommandManager only keeps a weak reference to the event handler, so a
  // strong handler must be maintained
  member private x._Handler with get () = _handler and set v = _handler <- v

  member x.RaiseCanExecuteChanged () = _canExecuteChanged.Trigger(x,EventArgs.Empty)

  interface IDisposable with
    member x.Dispose () =
      _execute <- None
      _canExecute <- None

  interface ICommand with
    [<CLIEvent>]
    member x.CanExecuteChanged = _canExecuteChanged.Publish
    member x.CanExecute p = _canExecute |> Option.apply p |> Option.defaultValue false
    member x.Execute p = _execute |> Option.apply p |> Option.defaultValue ()
