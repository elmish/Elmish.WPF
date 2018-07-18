namespace TwoWayList

open System
open System.Collections
open System.Collections.Generic

open Elmish
open Elmish.WPF

module App =    

    type Model = string * string list

    type Message =
        | ValueChanged of int * string
        | SingleValueChanged of string
        | Refresh

    let modelInit = "Value outside the list", [ "first value" ; "second value" ; "a third one" ; "and yet another" ; "and so on" ] 

    let init() = modelInit, Cmd.none
    
    let modelToList (model:Model): string list =
        let (_,list) = model
        list 

    let update msg model =
        let model' = 
            match msg with
            | Refresh -> model
            | SingleValueChanged newval ->
                newval, snd model
            | ValueChanged (index, newval) -> 
                let values =
                    model
                    |> modelToList
                    |> List.mapi (fun i oldval -> if i = index then newval else oldval)
                    
                fst model, values
        model', Cmd.none

    let listGetter m = (modelToList m) //:?> IEnumerable<string>
    let getter (model:Model) (key:string) =
        match Int32.TryParse key, key with
        | (true, index), _ -> (modelToList model).[index]
        | (false,_), "secondItem" -> (modelToList model).[1]
        | _,_ -> "Key error"
    let setter (value:string) _ (key:string) = 
        match Int32.TryParse key, key with
        | (true, index), _ -> ValueChanged(index, unbox value)
        | (false,_), "secondItem" -> ValueChanged(1, unbox value)
        | _,_ -> failwith "unexpected key"
        

    let singlegetter (v,_) = v
    let singlesetter v _ = v |> SingleValueChanged

    let view _ _ =
        [
            "SingleValue" |> Binding.twoWay singlegetter singlesetter
            //"Words" |> Binding.collectionTwoWay (modelToList >> List.mapi(fun i _ -> getter, setter) >> List.toSeq)
            "Words" |> Binding.collectionTwoWay (modelToList >> List.toSeq) getter setter
            "WordsWithEnumerator" |> Binding.collectionTwoWay (modelToList >> List.toSeq) getter setter
            "ReadonlyWords" |> Binding.oneWay (fun m -> modelToList m)
        ]

    //let start () =
    //    Program.mkProgram init update view
    //    |> Program.withConsoleTrace
    //    |> Program.runWindow (MainWindow())

