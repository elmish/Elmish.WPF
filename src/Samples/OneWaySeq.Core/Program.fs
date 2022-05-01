module Elmish.WPF.Samples.OneWaySeq.Program

open Serilog
open Serilog.Extensions.Logging
open Elmish.WPF


type Model =
  { OneWaySeqNumbers: int list
    OneWayNumbers: int list }

let init () =
  { OneWaySeqNumbers = [ 1000..-1..1 ]
    OneWayNumbers = [ 1000..-1..1 ] }

type Msg =
  | AddOneWaySeqNumber
  | AddOneWayNumber

let update msg m =
  match msg with
  | AddOneWaySeqNumber -> { m with OneWaySeqNumbers = m.OneWaySeqNumbers.Head + 1 :: m.OneWaySeqNumbers }
  | AddOneWayNumber -> { m with OneWayNumbers = m.OneWayNumbers.Head + 1 :: m.OneWayNumbers }

let bindings () : Binding<Model, Msg, obj> list = [
  "OneWaySeqNumbers" |> Binding.oneWaySeq((fun m -> m.OneWaySeqNumbers), (=), id)
  "OneWayNumbers" |> Binding.oneWay (fun m -> m.OneWayNumbers)
  "AddOneWaySeqNumber" |> Binding.cmd AddOneWaySeqNumber
  "AddOneWayNumber" |> Binding.cmd AddOneWayNumber
]

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
