namespace TreeViewModel

open System
open Elmish
open Elmish.WPF

module Model =
    type TreeModel =
        | Node of string * TreeModel list
        | Leaf of string

    let titleFrom = function
        | Node (t,_)  
        | Leaf t -> t
        
    let contentFrom = function
        | Node (_,content) -> content
        | Leaf _ -> []

    module LeafNode =
        type Message =
            | TitleModified of string
            | ContentModified of IndexOrKey * Message

        let rec update msg model =
            let model' =
                match msg, model with
                | TitleModified str, Node(_,content) -> Node(str, content)
                | TitleModified str, Leaf _ -> Leaf str
                | ContentModified(i,msg'), Node(t,content) -> Node (t, content |> List.mapi (fun j n -> if i = (Index j) then update msg' n else n))
                | ContentModified(i,msg'), Leaf t -> Leaf t
            
            model'

        let rec view() : ViewBindings<TreeModel,Message> =
            [   "Title" |> Binding.twoWay titleFrom (fun v m -> TitleModified v)
                "Content" |> Binding.collectionModel (fun m -> contentFrom m |> Seq.ofList) (view) (fun k msg -> ContentModified (k, msg) )
            ]

    let init() =
        let root =
            Node ("Root", [ Leaf ("Leaf 1")
                            Leaf ("Leaf 2") 
                            Node ("Node 3", [ Leaf "leaf 1.1"
                                              Leaf "leaf 1.2" ])
                            Node ("Node 4", [ Leaf "leaf 2.1" 
                                              Leaf "leaf 2.2" 
                                              Node ("Node 2.3", [ Leaf "leaf 2.3.1"
                                                                  Leaf "leaf 2.3.2" ])
                                              Leaf "leaf 2.4" ])
                          ] )                            
        [root], Cmd.none

    type Message = 
        | LeafNodeModified of IndexOrKey * LeafNode.Message

    let update msg (model:TreeModel list) =
        let model' =
            match msg with
            | LeafNodeModified (Index i, msg') -> LeafNode.update msg' model.[i] 
            | LeafNodeModified (Key _, _) -> failwith "Unexpected key"
        [ model' ], Cmd.none

    let view _ model : ViewBindings<TreeModel list,Message> =
        [
            "TreeData" |> Binding.collectionModel (fun m -> m |> Seq.ofList) (LeafNode.view) (fun k msg -> LeafNodeModified (k, msg))
            //"TreeDataReadonly" |> Binding.collectionModel (fun m -> m |> Seq.ofList) (LeafNode.view) (fun k msg -> LeafNodeModified (int k, msg))
            "RawTreeData" |> Binding.oneWay (fun m -> m)
        ]

module App =    
    open Model

    [<EntryPoint;STAThread>]
    let main argv = 
        Program.mkProgram init update view
        |> Program.withConsoleTrace        
        |> Program.runWindow (MainWindow())
       