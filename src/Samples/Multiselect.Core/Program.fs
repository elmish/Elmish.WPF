module Elmish.WPF.Samples.Multiselect.Program

open System
open Serilog
open Serilog.Extensions.Logging
open Elmish.WPF

type Entity =
  { Id: int
    Name: string
    IsSelected: bool }

type Model = { Entities: Entity list }

let init () =
  { Entities =
      [ 0..10 ]
      |> List.map (fun i ->
        { Id = i
          Name = sprintf "Entity %i" i
          IsSelected = i < 5 }) }

type Msg =
  | SetIsSelected of int * bool
  | DeselectAll

let update msg m =
  match msg with
  | SetIsSelected(entityId, isSelected) ->
    { m with
        Entities =
          m.Entities
          |> List.map (fun e ->
            if e.Id = entityId then
              { e with IsSelected = isSelected }
            else
              e) }
  | DeselectAll ->
    { m with
        Entities = m.Entities |> List.map (fun e -> { e with IsSelected = false }) }

let bindings () : Binding<Model, Msg> list =
  [ "SelectRandom"
    |> Binding.cmd (fun m ->
      m.Entities.Item(Random().Next(m.Entities.Length)).Id
      |> (fun id -> SetIsSelected(id, true)))

    "Deselect" |> Binding.cmd DeselectAll

    "Entities"
    |> Binding.subModelSeq (
      fun m -> m.Entities
      , fun e -> e.Id
      , fun () ->
        [ "Name" |> Binding.oneWay (fun (_, e) -> e.Name)
          "IsSelected"
          |> Binding.twoWay ((fun (_, e) -> e.IsSelected), (fun isSelected (_, e) -> SetIsSelected(e.Id, isSelected)))
          "SelectedLabel"
          |> Binding.oneWay (fun (_, e) -> if e.IsSelected then " - SELECTED" else "") ]
    ) ]

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