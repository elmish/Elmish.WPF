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
    | AddOneWaySeqNumber ->
        { m with
            OneWaySeqNumbers = m.OneWaySeqNumbers.Head + 1 :: m.OneWaySeqNumbers }
    | AddOneWayNumber ->
        { m with
            OneWayNumbers = m.OneWayNumbers.Head + 1 :: m.OneWayNumbers }

[<AllowNullLiteral>]
type OneWaySeqViewModel(args) =
    inherit ViewModelBase<Model, Msg>(args)

    member _.OneWaySeqNumbers =
        base.Get () (Binding.OneWaySeqT.id (=) id >> Binding.mapModel (fun m -> m.OneWaySeqNumbers))

    member _.OneWayNumbers =
        base.Get
            ()
            (Binding.OneWayT.id
             >> Binding.addLazy (=)
             >> Binding.mapModel (fun m -> m.OneWayNumbers))

    member _.AddOneWaySeqNumber = base.Get () (Binding.CmdT.setAlways AddOneWaySeqNumber)
    member _.AddOneWayNumber = base.Get () (Binding.CmdT.setAlways AddOneWayNumber)

let main window =
    let logger =
        LoggerConfiguration()
            .MinimumLevel.Override("Elmish.WPF.Update", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Bindings", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Performance", Events.LogEventLevel.Verbose)
            .WriteTo.Console()
            .CreateLogger()

    let createVm args = OneWaySeqViewModel(args)

    WpfProgram.mkSimpleT init update createVm
    |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))
    |> WpfProgram.startElmishLoop window