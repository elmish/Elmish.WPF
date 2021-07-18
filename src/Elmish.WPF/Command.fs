namespace Elmish.WPF

open System
open System.Windows.Input

/// A command that optionally hooks into CommandManager.RequerySuggested to
/// automatically trigger CanExecuteChanged whenever the CommandManager detects
/// conditions that might change the output of canExecute. It's necessary to use
/// this feature for command bindings where the CommandParameter is bound to
/// another UI control (e.g. a ListView.SelectedItem).
type internal Command(execute, canExecute, autoRequery) as this =

  let canExecuteChanged = Event<EventHandler,EventArgs>()

  // CommandManager only keeps a weak reference to the event handler,
  // so a strong reference must be maintained,
  // which is achieved by this mutable let-binding.
  // Can test this via the UiBoundCmdParam sample.
  let mutable handler = EventHandler(fun _ _ -> this.RaiseCanExecuteChanged())
  do if autoRequery then CommandManager.RequerySuggested.AddHandler handler

  member x.RaiseCanExecuteChanged () = canExecuteChanged.Trigger(x, EventArgs.Empty)

  interface ICommand with
    [<CLIEvent>]
    member _.CanExecuteChanged = canExecuteChanged.Publish
    member _.CanExecute p = canExecute p
    member _.Execute p = execute p