namespace Elmish.WPF

    
module ObservableCollection =
    open System.Collections.Specialized
    open System.Collections.ObjectModel

    /// Initialize observable collection
    let init n f = ObservableCollection<_>(List.init n f)

    /// Incremental map for observable collections
    let map f (oc:ObservableCollection<'T>) =
        // Create a resulting collection based on current elements
        let res = ObservableCollection<_>(Seq.map f oc)
        // Watch for changes in the source collection
        oc.CollectionChanged.Add(fun change ->
          printfn "%A" change.Action
          match change.Action with
          | NotifyCollectionChangedAction.Add ->
              // Apply 'f' to all new elements and add them to the result
              change.NewItems |> Seq.cast<'T> |> Seq.iteri (fun index item ->
                res.Insert(change.NewStartingIndex + index, f item))
          | NotifyCollectionChangedAction.Move ->
              // Move element in the resulting collection
              res.Move(change.OldStartingIndex, change.NewStartingIndex)
          | NotifyCollectionChangedAction.Remove ->
              // Remove element in the result
              res.RemoveAt(change.OldStartingIndex)
          | NotifyCollectionChangedAction.Replace -> 
              // Replace element with a new one (processed using 'f')
              change.NewItems |> Seq.cast<'T> |> Seq.iteri (fun index item ->
                res.[change.NewStartingIndex + index] <- f item)
          | NotifyCollectionChangedAction.Reset ->
              // Clear everything
              res.Clear()
          | _ -> failwith "Unexpected action!" )
        res

