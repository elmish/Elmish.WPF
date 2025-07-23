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

type MainViewModel(args) =
    inherit ViewModelBase<Model, Msg>(args)

    member _.Msg1 = base.Get () (Binding.OneWayT.id >> Binding.mapModel (fun m -> m.Msg1))
    member _.Msg2 = base.Get () (Binding.OneWayT.id >> Binding.mapModel (fun m -> m.Msg2))
    member _.GotFocus1 = base.Get () (Binding.CmdT.setAlways GotFocus1)
    member _.GotFocus2 = base.Get () (Binding.CmdT.setAlways GotFocus2)
    member _.LostFocus1 = base.Get () (Binding.CmdT.setAlways LostFocus1)
    member _.LostFocus2 = base.Get () (Binding.CmdT.setAlways LostFocus2)
    member _.ToggleVisibility = base.Get () (Binding.CmdT.setAlways ToggleVisibility)

    member _.ButtonText =
        base.Get () (Binding.OneWayT.id >> Binding.mapModel (fun m -> m.ButtonText))

    member _.TextBoxVisibility =
        base.Get () (Binding.OneWayT.id >> Binding.mapModel (fun m -> m.Visibility))

    member _.MouseMoveCommand =
        base.Get
            ()
            (Binding.CmdT.id false (fun p _ -> true)
             >> Binding.mapMsg paramToNewMousePositionMsg)

    member _.MousePosition =
        base.Get
            ()
            (Binding.OneWayT.id
             >> Binding.mapModel (fun m -> sprintf "%dx%d" m.MousePosition.X m.MousePosition.Y))

let designVm = MainViewModel(ViewModelArgs.simple (init ()))

let main window =

    let logger =
        LoggerConfiguration()
            .MinimumLevel.Override("Elmish.WPF.Update", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Bindings", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Performance", Events.LogEventLevel.Verbose)
            .WriteTo.Console()
            .CreateLogger()

    WpfProgram.mkSimpleT init update MainViewModel
    |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))
    |> WpfProgram.startElmishLoop window