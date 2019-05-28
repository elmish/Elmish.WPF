[<AutoOpen>]
module internal Elmish.WPF.InternalTypes

open System
open System.Windows.Input

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
