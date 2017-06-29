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
        | NumberToText of Guid
        | ItemMsg of ItemMsg

    type Model = 
        { Items: ItemModel list }


module State =
    open Types
     
    let init() = { Items = [] }

    let numberToText item = { item with Text = sprintf "Number = %d" item.Number }

    let itemUpdate (msg:ItemMsg) (model:ItemModel) =
        match msg with
        | ItemNumberToText id -> { model with Text = sprintf "Number = %d" model.Number }

    let update (msg:Msg) (model:Model) =
        match msg with
        | AddItems -> 
            let numToAdd = 1000
            let newItems = 
                [ model.Items.Length .. model.Items.Length + numToAdd ]
                |> List.map (fun i -> { Id = Guid.NewGuid(); Text = "Nothing yet"; Number = i })
            { model with Items = newItems |> List.append model.Items }
        | NumberToText id -> { model with Items = model.Items |> List.map (fun i -> if i.Id = id then numberToText i else i)}
        | ItemMsg msg -> { model with Items = model.Items |> List.map (fun m -> itemUpdate msg m) }
        

module App =
    open State
    open Types

    let view _ _ = 
        [ "ArrayItems" |> Binding.oneWay (fun m -> m.Items)
          "ListItems" |> Binding.oneWay (fun m -> m.Items)
          "AddItems" |> Binding.cmd (fun _ _ -> AddItems)
          "NumToText" |> Binding.cmd (fun p m -> console.log(p); p :?> Guid |> NumberToText)
          "ItemNumToText" |> Binding.cmd (fun p m -> console.log(p); p :?> Guid |> NumberToText) ]

    [<EntryPoint;STAThread>]
    let main argv = 
        Program.mkSimple init update view
//        |> Program.withConsoleTrace
//        |> Program.withSubscription subscribe
        |> Program.runWindow (Elmish.Samples.PerformanceViews.MainWindow())