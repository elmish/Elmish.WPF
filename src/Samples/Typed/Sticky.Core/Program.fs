module Elmish.WPF.Samples.Sticky.Program

open Serilog
open Serilog.Extensions.Logging
open Elmish.WPF

type Model = { Count: int; StepSize: int }

type Msg =
    | Increment
    | Decrement
    | SetStepSize of int
    | Reset

let init () = { Count = 0; StepSize = 1 }

let canReset m = m <> init ()

let update msg m =
    match msg with
    | Increment -> { m with Count = m.Count + m.StepSize }
    | Decrement -> { m with Count = m.Count - m.StepSize }
    | SetStepSize x -> { m with StepSize = x }
    | Reset -> init ()

[<AllowNullLiteral>]
type StickyViewModel(args) =
    inherit ViewModelBase<Model, Msg>(args)

    let stepSizeBinding =
        Binding.TwoWayT.id
        >> Binding.addLazy (=)
        >> Binding.mapModel (fun (m: Model) -> float m.StepSize)
        >> Binding.mapMsg (int >> SetStepSize)

    member _.CounterValue =
        base.Get
            ()
            (Binding.OneWayT.id
             >> Binding.addSticky (fun v -> v % 2 = 0)
             >> Binding.addLazy (=)
             >> Binding.mapModel (fun m -> m.Count))

    member _.Increment = base.Get () (Binding.CmdT.setAlways Increment)
    member _.Decrement = base.Get () (Binding.CmdT.setAlways Decrement)

    member this.StepSize
        with get () = base.Get () stepSizeBinding
        and set (value) = base.Set (value) stepSizeBinding

    member _.Reset = base.Get () (Binding.CmdT.set canReset Reset)

let main window =
    let logger =
        LoggerConfiguration()
            .MinimumLevel.Override("Elmish.WPF.Update", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Bindings", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Performance", Events.LogEventLevel.Verbose)
            .WriteTo.Console()
            .CreateLogger()

    let createVm args = StickyViewModel(args)

    WpfProgram.mkSimpleT init update createVm
    |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))
    |> WpfProgram.startElmishLoop window