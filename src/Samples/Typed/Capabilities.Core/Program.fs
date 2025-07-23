module Elmish.WPF.Samples.Capabilities.Program

open System.Windows
open Serilog
open Serilog.Extensions.Logging
open Elmish.WPF
open Selection

type Screen = SelectionScreen

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

    let update =
        function
        | SetVisibleScreen s -> s |> VisibleScreen.set
        | SelectionMsg msg -> msg |> Selection.update

    let boolToVis =
        function
        | true -> Visibility.Visible
        | false -> Visibility.Collapsed

[<AllowNullLiteral>]
type MainViewModel(args) =
    inherit ViewModelBase<Model, Msg>(args)

    let selectionVm (args: ViewModelArgs<Selection, SelectionMsg>) = SelectionViewModel(args)

    member _.Selection =
        base.Get
            ()
            (Binding.SubModelT.req selectionVm
             >> Binding.mapModel Program.Selection.get
             >> Binding.mapMsg SelectionMsg)

    member _.ShowSelection =
        base.Get () (Binding.CmdT.setAlways (SelectionScreen |> Some |> SetVisibleScreen))

    member _.SelectionVisibility =
        base.Get
            ()
            (Binding.OneWayT.id
             >> Binding.mapModel (Program.VisibleScreen.get >> (=) (Some SelectionScreen) >> Program.boolToVis))

let main window =
    let logger =
        LoggerConfiguration()
            .MinimumLevel.Override("Elmish.WPF.Update", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Bindings", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Performance", Events.LogEventLevel.Verbose)
            .WriteTo.Console()
            .CreateLogger()

    let createVm args = MainViewModel(args)

    WpfProgram.mkSimpleT (fun () -> Program.init) Program.update createVm
    |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))
    |> WpfProgram.startElmishLoop window