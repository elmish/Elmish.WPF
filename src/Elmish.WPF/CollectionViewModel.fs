module internal Elmish.WPF.CollectionViewModel 

open System.Dynamic
open System.Collections
open System.Collections.Generic
open System.Collections.Specialized
open System.ComponentModel

type ItemViewModel(getter, setter) =
    inherit DynamicObject()

    override __.TryGetMember (binder, r) = 
        if binder.Name = "Value" then
            r <- getter 
            true
        else
            false

    override __.TrySetMember (binder, o) = 
        if binder.Name = "Value" then
            setter o
            true
        else
            false   

type ListViewModel<'model,'msg>(name, items: seq<obj>, getter: IndexOrKey -> obj, setter: obj -> IndexOrKey -> unit) =
    inherit DynamicObject()  

    interface IEnumerable with
        member this.GetEnumerator() =
            (items
            |> Seq.mapi (fun i _ -> ItemViewModel(getter (Index i), fun v -> setter v (Index i)))
            :> IEnumerable).GetEnumerator()

    override __.TryGetIndex (binder, indexes, value) =
        let key = indexes.[0] |> string   
        match System.Int32.TryParse(key) with
        | (true, i) -> value <- getter (Index i)
        | (false,_) -> value <- getter (Key key)
        true

    override this.TrySetIndex (binder, indexes, value) =            
        let key = indexes.[0] |> string
        try
            match System.Int32.TryParse(key) with
            | (true, i) -> setter value (Index i)
            | (false,_) -> setter value (Key key)
            
        with | _ -> ()
        true

    
//type ModelViewModel<'model,'msg> (name, models: seq<INotifyPropertyChanged>) =
//    inherit DynamicObject()

//    do printfn "ModelViewModel %s starting" name

//    //let collChanged = new Event<_,_>()

//    let notify i = 
//        new PropertyChangedEventHandler( fun sender args -> printfn "never there... why ? %d" i)

//    let items = new List<INotifyPropertyChanged>()

//    do  
//        models
//        |> Seq.iteri (fun i x ->
//                                items.Add x
//                                x.PropertyChanged.AddHandler (notify i))

//    interface IEnumerable with
//        member this.GetEnumerator() =
//            (models 
//            |> Seq.mapi (fun i x ->
//                        //x.PropertyChanged.AddHandler (notify i)
//                        //items.Add x
//                        x)
//            :> IEnumerable).GetEnumerator()    

//    //interface INotifyCollectionChanged with
//    //    [<CLIEvent>]
//    //    member this.CollectionChanged = collChanged.Publish // raise (System.NotImplementedException())

    //override __.Finalize() =
    //    printfn "ModelViewModel %s ending" name
        

 
        


      


