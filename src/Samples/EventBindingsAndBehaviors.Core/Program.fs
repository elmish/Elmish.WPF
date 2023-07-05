module Elmish.WPF.Samples.EventBindingsAndBehaviors.Program

open System.Windows
open System.Windows.Input
open Serilog
open Serilog.Extensions.Logging
open Elmish.WPF


type Position = { X: int; Y: int }

type Model =
  { Msg1: string
    Msg2: string
    ButtonText: string
    Visibility: Visibility
    MousePosition: Position }

let visibleButtonText = "Hide text box"
let hiddenButtonText = "Show text box"

let init () =
  { Msg1 = ""
    Msg2 = ""
    ButtonText = visibleButtonText
    Visibility = Visibility.Visible
    MousePosition = { X = 0; Y = 0 } }

type Msg =
  | GotFocus1
  | GotFocus2
  | LostFocus1
  | LostFocus2
  | ToggleVisibility
  | NewMousePosition of Position

let update msg m =
  match msg with
  | GotFocus1 -> { m with Msg1 = "Focused" }
  | GotFocus2 -> { m with Msg2 = "Focused" }
  | LostFocus1 -> { m with Msg1 = "Not focused" }
  | LostFocus2 -> { m with Msg2 = "Not focused" }
  | ToggleVisibility ->
    if m.Visibility = Visibility.Visible then
      { m with
          Visibility = Visibility.Hidden
          ButtonText = hiddenButtonText }
    else
      { m with
          Visibility = Visibility.Visible
          ButtonText = visibleButtonText }
  | NewMousePosition p -> { m with MousePosition = p }


let paramToNewMousePositionMsg (p: obj) =
  let args = p :?> MouseEventArgs
  let e = args.OriginalSource :?> UIElement
  let point = args.GetPosition e
  NewMousePosition { X = int point.X; Y = int point.Y }

let bindings () : Binding<Model, Msg> list =
  [ "Msg1" |> Binding.oneWay (fun m -> m.Msg1)
    "Msg2" |> Binding.oneWay (fun m -> m.Msg2)
    "GotFocus1" |> Binding.cmd GotFocus1
    "GotFocus2" |> Binding.cmd GotFocus2
    "LostFocus1" |> Binding.cmd LostFocus1
    "LostFocus2" |> Binding.cmd LostFocus2
    "ToggleVisibility" |> Binding.cmd ToggleVisibility
    "ButtonText" |> Binding.oneWay (fun m -> m.ButtonText)
    "TextBoxVisibility" |> Binding.oneWay (fun m -> m.Visibility)
    "MouseMoveCommand" |> Binding.cmdParam paramToNewMousePositionMsg
    "MousePosition"
    |> Binding.oneWay (fun m -> sprintf "%dx%d" m.MousePosition.X m.MousePosition.Y) ]

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