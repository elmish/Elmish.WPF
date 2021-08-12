module Elmish.WPF.Samples.Capabilities.Program

open System.Windows

open Serilog
open Serilog.Extensions.Logging

open Elmish.WPF

open Selection


type Screen =
  SelectionScreen

type Model =
  { VisibleScreen: Screen option
    Selection: Selection }

type Msg =
  | SetVisibleScreen of Screen option
  | SelectionMsg of SelectionMsg


module Program =
  module VisibleScreen =
    let get m = m.VisibleScreen
    let set v m = { m with VisibleScreen = v }
  module Selection =
    open Selection
    let get m = m.Selection
    let set v m = { m with Selection = v }
    let map = map get set
    let update = update >> map

  let init =
    { VisibleScreen = None
      Selection = Selection.init }

  let update = function
    | SetVisibleScreen s -> s |> VisibleScreen.set
    | SelectionMsg msg -> msg |> Selection.update

  let boolToVis = function
    | true  -> Visibility.Visible
    | false -> Visibility.Collapsed

  let bindings () = [
    "Selection"
      |> Binding.SubModel.required Selection.bindings
      |> Binding.mapModel Selection.get
      |> Binding.mapMsg SelectionMsg
    "ShowSelection" |> Binding.cmd (SelectionScreen |> Some |> SetVisibleScreen)
    "SelectionVisibility" |> Binding.oneWay (VisibleScreen.get >> (=) (Some SelectionScreen) >> boolToVis)
  ]


let main window =

  let logger =
    LoggerConfiguration()
      .MinimumLevel.Override("Elmish.WPF.Update", Events.LogEventLevel.Verbose)
      .MinimumLevel.Override("Elmish.WPF.Bindings", Events.LogEventLevel.Verbose)
      .MinimumLevel.Override("Elmish.WPF.Performance", Events.LogEventLevel.Verbose)
      .WriteTo.Console()
      .CreateLogger()

  WpfProgram.mkSimple (fun () -> Program.init) Program.update Program.bindings
  |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))
  |> WpfProgram.startElmishLoop window