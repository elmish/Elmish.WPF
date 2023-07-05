module Elmish.WPF.Samples.SubModelSelectedItem.Program

open System
open Serilog
open Serilog.Extensions.Logging
open Elmish.WPF

type Entity = { Id: int; Name: string }

type Model =
  { Entities: Entity list
    Selected: int option }

let init () =
  { Entities = [ 0..10 ] |> List.map (fun i -> { Id = i; Name = sprintf "Entity %i" i })
    Selected = Some 4 }

type Msg = Select of int option

let update msg m =
  match msg with
  | Select entityId -> { m with Selected = entityId }

let bindings () : Binding<Model, Msg> list =
  [ "SelectRandom"
    |> Binding.cmd (fun m -> m.Entities.Item(Random().Next(m.Entities.Length)).Id |> Some |> Select)

    "Deselect" |> Binding.cmd (Select None)

    "Entities"
    |> Binding.subModelSeq (
      (fun m -> m.Entities),
      (fun e -> e.Id),
      (fun () ->
        [ "Name" |> Binding.oneWay (fun (_, e) -> e.Name)
          "SelectedLabel"
          |> Binding.oneWay (fun (m, e) -> if m.Selected = Some e.Id then " - SELECTED" else "") ])
    )

    "SelectedEntity"
    |> Binding.subModelSelectedItem ("Entities", (fun m -> m.Selected), Select) ]

let designVm = ViewModel.designInstance (init ()) (bindings ())

let main window =
  let logger =
    LoggerConfiguration()
      .MinimumLevel.Override("Elmish.WPF.Update", Events.LogEventLevel.Verbose)
      .MinimumLevel.Override("Elmish.WPF.Bindings", Events.LogEventLevel.Verbose)
      .MinimumLevel.Override("Elmish.WPF.Performance", Events.LogEventLevel.Verbose)
      .WriteTo.Console()
      .CreateLogger()

  WpfProgram.mkSimple init update bindings
  |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))
  |> WpfProgram.startElmishLoop window