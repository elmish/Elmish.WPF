namespace TreeViewModel

open System
open Elmish
open Elmish.WPF

module Model =
    type TreeViewModel =
        | Expanded of string * TreeViewModel list
        | Collapsed of string * TreeViewModel list
        | Leaf of string

    let titleFrom = function
        | Expanded (title,_) 
        | Collapsed (title,_) 
        | Leaf title -> title
        
    let contentFrom = function
        | Expanded (_,content) 
        | Collapsed (_,content) -> content
        | Leaf _ -> []

    let isExpanded = function 
        | Expanded _ -> true
        | _ -> false

    module LeafNode =
        type Message =
            | TitleModified of string
            | ContentModified of int * Message
            | Expand of bool

        let rec update msg model =
            let model' =
                match msg, model with
                | TitleModified str, Expanded(_,content) -> Expanded(str, content)
                | TitleModified str, Collapsed(_,content) -> Collapsed(str, content)
                | TitleModified str, Leaf _ -> Leaf str
                | ContentModified(i,msg'), Expanded(t,content) -> Expanded (t, content |> List.mapi (fun j n -> if i = j then update msg' n else n))
                | ContentModified(i,msg'), Collapsed(t,content) -> Collapsed (t, content |> List.mapi (fun j n -> if i = j then update msg' n else n))
                | ContentModified(i,msg'), Leaf t -> Leaf t
                | Expand true, Expanded (title,content) 
                | Expand true, Collapsed (title,content)-> Expanded (title,content)
                | Expand false, Expanded (title,content) 
                | Expand false, Collapsed (title,content) -> Collapsed (title,content)
                | Expand _, Leaf title -> Leaf title
            
            model'

        let rec view() : ViewBindings<TreeViewModel,Message> =
            [   
                "IsExpanded" |> Binding.twoWay isExpanded (fun v _ -> Expand v)
                "Title" |> Binding.twoWay titleFrom (fun v m -> TitleModified v)
                "Content" |> Binding.collectionModel (fun m -> contentFrom m |> Seq.ofList) (view) (fun k msg -> ContentModified (k, msg) )
            ]

    let init() =
        let root =
            Expanded ("Root", [ Leaf ("Leaf 1")
                                Leaf ("Leaf 2") 
                                Expanded ("Node 3", [ Leaf "leaf 1.1"
                                                      Leaf "leaf 1.2" ])
                                Expanded ("Node 4", [ Leaf "leaf 2.1" 
                                                      Leaf "leaf 2.2" 
                                                      Collapsed ("Node 2.3", [ Leaf "leaf 2.3.1"
                                                                               Leaf "leaf 2.3.2" ])
                                                      Leaf "leaf 2.4" ])
                          ] )                            
        [root], Cmd.none

    type Message = 
        | LeafNodeModified of int * LeafNode.Message

    let update msg (model:TreeViewModel list) =
        let model' =
            match msg with
            | LeafNodeModified (i, msg') -> LeafNode.update msg' model.[i] 
        [ model' ], Cmd.none

    let view _ model : ViewBindings<TreeViewModel list,Message> =
        [
            "TreeData" |> Binding.collectionModel (fun m -> m |> Seq.ofList) (LeafNode.view) (fun k msg -> LeafNodeModified (k, msg))
            "TreeDataMirror" |> Binding.collectionModel (fun m -> m |> Seq.ofList) (LeafNode.view) (fun k msg -> LeafNodeModified (k, msg))
            "RawTreeData" |> Binding.oneWay (fun m -> m)
        ]

module App =    
    open Model

    [<EntryPoint;STAThread>]
    let main argv = 
        Program.mkProgram init update view
        |> Program.withConsoleTrace        
        |> Program.runWindow (MainWindow())
