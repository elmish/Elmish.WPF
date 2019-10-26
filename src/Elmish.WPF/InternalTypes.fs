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


    
open System.Collections.ObjectModel
open System.ComponentModel
open System.Collections.Specialized


type EnhancedObservableCollection<'a> (seq: 'a seq) =
  inherit ObservableCollection<'a>(seq)

  let items = base.Items :?> System.Collections.Generic.List<'a> // hack

  let PropertyChangedEventArgsCount = "Count" |> PropertyChangedEventArgs
  let PropertyChangedEventArgsIndexer = "Item[]" |> PropertyChangedEventArgs
  

  member _.OnPropertyChangedCount () = base.OnPropertyChanged PropertyChangedEventArgsCount
  member _.OnPropertyChangedIndexer () = base.OnPropertyChanged PropertyChangedEventArgsIndexer
  member _.OnCollectionChangedItemRemoved nccea = base.OnCollectionChanged nccea

  member this.RemoveAll (predicate: 'a -> int option) =
    let predicateFunc a =
      let index = predicate a
      index |> Option.iter (fun i ->
        this.OnPropertyChangedCount ()
        this.OnPropertyChangedIndexer ()
        this.OnCollectionChangedItemRemoved <| NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, a, i)
      )
      index.IsSome
    let predicate = Predicate<'a>(predicateFunc)
    items.RemoveAll predicate |> ignore
