module Elmish.WPF.Samples.SingleCounter.Program

open Serilog
open Serilog.Extensions.Logging
open Elmish.WPF

module Counter =
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
type CounterViewModel(args) =
    inherit ViewModelBase<Counter.Model, Counter.Msg>(args)

    let stepSizeBinding =
        Binding.TwoWayT.id
        >> Binding.addLazy (=)
        >> Binding.mapModel (fun (m: Counter.Model) -> float m.StepSize)
        >> Binding.mapMsg (int >> Counter.SetStepSize)

    member _.CounterValue =
        base.Get () (Binding.OneWayT.id >> Binding.addLazy (=) >> Binding.mapModel (fun m -> m.Count))

    member _.Increment = base.Get () (Binding.CmdT.setAlways Counter.Increment)
    member _.Decrement = base.Get () (Binding.CmdT.setAlways Counter.Decrement)

    member this.StepSize
        with get () = base.Get () stepSizeBinding
        and set (value) = base.Set (value) stepSizeBinding

    member _.Reset = base.Get () (Binding.CmdT.set Counter.canReset Counter.Reset)

let main window =
    let logger =
        LoggerConfiguration()
            .MinimumLevel.Override("Elmish.WPF.Update", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Bindings", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Performance", Events.LogEventLevel.Verbose)
            .WriteTo.Console()
            .CreateLogger()

    let createVm args = CounterViewModel(args)

    WpfProgram.mkSimpleT Counter.init Counter.update createVm
    |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))
    |> WpfProgram.startElmishLoop window