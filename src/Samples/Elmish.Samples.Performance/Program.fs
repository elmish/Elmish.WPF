namespace Elmish.Samples.Performance

open System
open Elmish
open Elmish.WPF

module Types =

    type ItemModel =
        { Id: Guid
          Text: string
          Number: int }

    type ItemMsg =
        | ItemNumberToText of Guid

    type Msg =
        | AddItems
        | RemoveItem of Guid
        | NumberToText of Guid
        | ItemMsg of ItemMsg

    type Model = 
        { Items: ItemModel list }


module State =
    open Types
     
    let init() = { Items = [] }, Cmd.none

    let numberToText item = { item with Text = sprintf "Number = %d" item.Number }

    let itemUpdate (msg:ItemMsg) (model:ItemModel) =
        match msg with
        | ItemNumberToText id -> { model with Text = sprintf "Number = %d" model.Number }

    let update (model:Model) = function
        | AddItems -> 
            let numToAdd = 1000
            let newItems = 
                [ model.Items.Length .. model.Items.Length + numToAdd ]
                |> List.map (fun i -> { Id = Guid.NewGuid(); Text = "Nothing yet"; Number = i })
            { model with Items = newItems |> List.append model.Items }, Cmd.none
        | RemoveItem id -> { model with Items = model.Items |> List.filter (fun i -> i.Id <> id) }, Cmd.none
        | NumberToText id -> { model with Items = model.Items |> List.map (fun i -> if i.Id = id then numberToText i else i)}, Cmd.none
        | ItemMsg msg -> { model with Items = model.Items |> List.map (fun m -> itemUpdate msg m) }, Cmd.none
        

module App =
    open State
    open Types
    open System.Windows

    let remove i = RemoveItem i.Id

    let view _ _ = 
        [ "ArrayItems" |> Binding.oneWay (fun m -> m.Items)
          "ListItems" |> Binding.oneWay (fun m -> m.Items)
          "AddItems" |> Binding.cmd (fun _ _ -> AddItems)
          "RemoveItem" |> Binding.cmdIf (fun p _ -> p :?> ItemModel |> remove ) (fun p _ -> p <> null)
          "NumItems" |> Binding.oneWayMap (fun m -> m.Items.Length) (sprintf "%d items!")
          "NumToText" |> Binding.cmd (fun p m -> console.log(p); p :?> Guid |> NumberToText)
          "ItemNumToText" |> Binding.cmd (fun p m -> console.log(p); p :?> Guid |> NumberToText) ]

    [<EntryPoint;STAThread>]
    let main argv = 
        Program.mkProgram init update view
        |> Program.withErrorHandler (fun (_, ex) -> MessageBox.Show(ex.Message) |> ignore)
//        |> Program.withConsoleTrace
//        |> Program.withSubscription subscribe
        |> Program.runWindow (Elmish.Samples.PerformanceViews.MainWindow())