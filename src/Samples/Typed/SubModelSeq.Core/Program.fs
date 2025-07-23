module Elmish.WPF.Samples.SubModelSeq.Program

open System
open Serilog
open Serilog.Extensions.Logging
open Elmish.WPF

module Counter =
    type Model = { Id: Guid; Count: int; StepSize: int }

    type Msg =
        | Increment
        | Decrement
        | SetStepSize of int
        | Reset

    let init id = { Id = id; Count = 0; StepSize = 1 }

    let canReset m = m.Count <> 0 || m.StepSize <> 1

    let update msg m =
        match msg with
        | Increment -> { m with Count = m.Count + m.StepSize }
        | Decrement -> { m with Count = m.Count - m.StepSize }
        | SetStepSize x -> { m with StepSize = x }
        | Reset -> { m with Count = 0; StepSize = 1 }

[<AllowNullLiteral>]
type CounterViewModel(args) =
    inherit ViewModelBase<Counter.Model, Counter.Msg>(args)

    member _.CounterId =
        base.Get () (Binding.OneWayT.id >> Binding.addLazy (=) >> Binding.mapModel (fun m -> m.Id))

    member _.CounterValue =
        base.Get () (Binding.OneWayT.id >> Binding.addLazy (=) >> Binding.mapModel (fun m -> m.Count))

    member _.Increment = base.Get () (Binding.CmdT.setAlways Counter.Increment)
    member _.Decrement = base.Get () (Binding.CmdT.setAlways Counter.Decrement)

    member _.StepSize =
        base.Get
            ()
            (Binding.TwoWayT.id
             >> Binding.addLazy (=)
             >> Binding.mapModel (fun (m: Counter.Model) -> float m.StepSize)
             >> Binding.mapMsg (int >> Counter.SetStepSize))

    member _.Reset = base.Get () (Binding.CmdT.set Counter.canReset Counter.Reset)

module App =
    type Model =
        { Counters: Counter.Model list
          GlobalState: bool }

    type CounterOutMsg = | Remove

    type Msg =
        | AddCounter
        | RemoveCounter of Guid
        | CounterMsg of Guid * Counter.Msg
        | ToggleGlobalState

    let init () =
        { Counters = [ Counter.init (Guid.NewGuid()) ]
          GlobalState = false }

    let update msg m =
        match msg with
        | AddCounter ->
            { m with
                Counters = m.Counters @ [ Counter.init (Guid.NewGuid()) ] }
        | RemoveCounter id ->
            { m with
                Counters = m.Counters |> List.filter (fun c -> c.Id <> id) }
        | CounterMsg(id, counterMsg) ->
            { m with
                Counters =
                    m.Counters
                    |> List.map (fun c -> if c.Id = id then Counter.update counterMsg c else c) }
        | ToggleGlobalState ->
            { m with
                GlobalState = not m.GlobalState }

type InOutMsg<'a, 'b> =
    | InMsg of 'a
    | OutMsg of 'b

[<AllowNullLiteral>]
type CounterWithRemoveViewModel(args) =
    inherit ViewModelBase<Counter.Model, InOutMsg<Counter.Msg, App.CounterOutMsg>>(args)

    let stepSizeBinding =
        Binding.TwoWayT.id
        >> Binding.addLazy (=)
        >> Binding.mapModel (fun (m: Counter.Model) -> float m.StepSize)
        >> Binding.mapMsg (int >> Counter.SetStepSize >> InMsg)

    member _.CounterId =
        base.Get () (Binding.OneWayT.id >> Binding.addLazy (=) >> Binding.mapModel (fun m -> m.Id))

    member _.CounterValue =
        base.Get () (Binding.OneWayT.id >> Binding.addLazy (=) >> Binding.mapModel (fun m -> m.Count))

    member _.Increment = base.Get () (Binding.CmdT.setAlways (InMsg Counter.Increment))
    member _.Decrement = base.Get () (Binding.CmdT.setAlways (InMsg Counter.Decrement))

    member this.StepSize
        with get () = base.Get () stepSizeBinding
        and set (value) = base.Set (value) stepSizeBinding

    member _.Reset = base.Get () (Binding.CmdT.set Counter.canReset (InMsg Counter.Reset))

    member _.Remove =
        base.Get () (Binding.CmdT.setAlways (OutMsg App.CounterOutMsg.Remove))

[<AllowNullLiteral>]
type MainViewModel(args) =
    inherit ViewModelBase<App.Model, App.Msg>(args)

    let createCounterVm (args: ViewModelArgs<Counter.Model, InOutMsg<Counter.Msg, App.CounterOutMsg>>) =
        CounterWithRemoveViewModel(args)

    member _.Counters =
        base.Get
            ()
            (Binding.SubModelSeqKeyedT.id createCounterVm (fun (m: Counter.Model) -> m.Id)
             >> Binding.mapModel (fun (m: App.Model) -> m.Counters)
             >> Binding.mapMsg (fun (id, msg) ->
                 match msg with
                 | InMsg counterMsg -> App.CounterMsg(id, counterMsg)
                 | OutMsg App.CounterOutMsg.Remove -> App.RemoveCounter id))

    member _.AddCounter = base.Get () (Binding.CmdT.setAlways App.AddCounter)
    member _.ToggleGlobalState = base.Get () (Binding.CmdT.setAlways App.ToggleGlobalState)

    member _.GlobalState =
        base.Get
            ()
            (Binding.OneWayT.id
             >> Binding.addLazy (=)
             >> Binding.mapModel (fun m -> m.GlobalState))

let main window =
    let logger =
        LoggerConfiguration()
            .MinimumLevel.Override("Elmish.WPF.Update", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Bindings", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Performance", Events.LogEventLevel.Verbose)
            .WriteTo.Console()
            .CreateLogger()

    let createVm args = MainViewModel(args)

    WpfProgram.mkSimpleT App.init App.update createVm
    |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))
    |> WpfProgram.startElmishLoop window