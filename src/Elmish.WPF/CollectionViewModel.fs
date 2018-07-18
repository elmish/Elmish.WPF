module internal Elmish.WPF.CollectionViewModel 

open System.Dynamic
open System.Collections

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

type ListViewModel<'model,'msg>(name, items: seq<obj>, getter: string -> obj, setter: obj -> string -> unit) =
    inherit DynamicObject()  

    interface IEnumerable with
        member this.GetEnumerator() =
            (items
            |> Seq.mapi (fun i _ -> ItemViewModel(getter (string i), fun v -> setter v (string i)))
            :> IEnumerable).GetEnumerator()

    override __.TryGetIndex (binder, indexes, value) =
        let key = indexes.[0] |> string        
        value <- getter key        
        true

    override this.TrySetIndex (binder, indexes, value) =            
        let key = indexes.[0] |> string
        try
            setter value key
        with | _ -> ()
        true

    
type ModelViewModel<'model,'msg> (name, models: seq<_>) =
    inherit DynamicObject()

    interface IEnumerable with
        member this.GetEnumerator() =
            (models 
            |> Seq.map (fun x -> unbox x  )
            :> IEnumerable).GetEnumerator()
        

 
        


      


