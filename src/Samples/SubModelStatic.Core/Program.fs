namespace Elmish.WPF.Samples.SubModelStatic

open System
open Serilog
open Serilog.Extensions.Logging
open Elmish
open Elmish.WPF

module Counter =

  type Model =
    { Count: int
      StepSize: int }

  type Msg =
    | Increment
    | Decrement
    | SetStepSize of int
    | Reset

  let init =
    { Count = 0
      StepSize = 1 }

  let canReset = (<>) init

  let update msg m =
    match msg with
    | Increment -> { m with Count = m.Count + m.StepSize }
    | Decrement -> { m with Count = m.Count - m.StepSize }
    | SetStepSize x -> { m with StepSize = x }
    | Reset -> init

type [<AllowNullLiteral>] CounterViewModel (args) =
  inherit ViewModelBase<Counter.Model, Counter.Msg>(args)

  new() = CounterViewModel(Counter.init |> ViewModelArgs.simple)

  member _.StepSize
    with get() = base.Get() (Binding.OneWayT.id >> Binding.addLazy (=) >> Binding.mapModel (fun m -> m.StepSize))
    and set(v) = base.Set(v) (Binding.OneWayToSourceT.id >> Binding.mapMsg Counter.Msg.SetStepSize)
  member _.CounterValue = base.Get() (Binding.OneWayT.id >> Binding.addLazy (=) >> Binding.mapModel (fun m -> m.Count))
  member _.Increment = base.Get() (Binding.cmd Counter.Increment)
  member _.Decrement = base.Get() (Binding.cmd Counter.Decrement)
  member _.Reset = base.Get() (Binding.cmdIf (Counter.Reset, Counter.canReset))


module Clock =

  type TimeType =
    | Utc
    | Local

  type Model =
    { Time: DateTimeOffset
      TimeType: TimeType }

  let init () =
    { Time = DateTimeOffset.Now
      TimeType = Local }

  let getTime m =
    match m.TimeType with
    | Utc -> m.Time.UtcDateTime
    | Local -> m.Time.LocalDateTime

  type Msg =
    | Tick of DateTimeOffset
    | SetTimeType of TimeType

  let update msg m =
    match msg with
    | Tick t -> { m with Time = t }
    | SetTimeType t -> { m with TimeType = t }

type [<AllowNullLiteral>] ClockViewModel (args) =
  inherit ViewModelBase<Clock.Model, Clock.Msg>(args)
  
  new() = ClockViewModel(Clock.init () |> ViewModelArgs.simple)

  member _.Time = base.Get() (Binding.OneWayT.id >> Binding.addLazy (=) >> Binding.mapModel Clock.getTime)
  member _.IsLocal = base.Get() (Binding.OneWayT.id >> Binding.addLazy (=) >> Binding.mapModel (fun m -> m.TimeType = Clock.Local))
  member _.SetLocal = base.Get() (Binding.cmd (Clock.SetTimeType Clock.Local))
  member _.IsUtc = base.Get() (Binding.OneWayT.id >> Binding.addLazy (=) >> Binding.mapModel (fun m -> m.TimeType = Clock.Utc))
  member _.SetUtc = base.Get() (Binding.cmd (Clock.SetTimeType Clock.Utc))


module CounterWithClock =

  type Model =
    { Counter: Counter.Model
      Clock: Clock.Model }

  module ModelM =
    module Counter =
      let get m = m.Counter
    module Clock =
      let get m = m.Clock

  let init () =
    { Counter = Counter.init
      Clock = Clock.init () }

  type Msg =
    | CounterMsg of Counter.Msg
    | ClockMsg of Clock.Msg

  let update msg m =
    match msg with
    | CounterMsg msg -> { m with Counter = Counter.update msg m.Counter }
    | ClockMsg msg -> { m with Clock = Clock.update msg m.Clock }

type [<AllowNullLiteral>] CounterWithClockViewModel (args) =
  inherit ViewModelBase<CounterWithClock.Model, CounterWithClock.Msg>(args)
  
  new() = CounterWithClockViewModel(CounterWithClock.init () |> ViewModelArgs.simple)

  member _.Counter = base.Get() (Binding.SubModelT.req CounterViewModel >> Binding.mapModel CounterWithClock.ModelM.Counter.get >> Binding.mapMsg CounterWithClock.CounterMsg)
  member _.Clock = base.Get() (Binding.SubModelT.req ClockViewModel >> Binding.mapModel CounterWithClock.ModelM.Clock.get >> Binding.mapMsg CounterWithClock.ClockMsg)


module App2 =

  type Model =
    { ClockCounter1: CounterWithClock.Model
      ClockCounter2: CounterWithClock.Model }

  module ModelM =
    module ClockCounter1 =
      let get m = m.ClockCounter1
    module ClockCounter2 =
      let get m = m.ClockCounter2

  let init () =
    { ClockCounter1 = CounterWithClock.init ()
      ClockCounter2 = CounterWithClock.init () }

  type Msg =
    | ClockCounter1Msg of CounterWithClock.Msg
    | ClockCounter2Msg of CounterWithClock.Msg

  let update msg m =
    match msg with
    | ClockCounter1Msg msg ->
        { m with ClockCounter1 = CounterWithClock.update msg m.ClockCounter1 }
    | ClockCounter2Msg msg ->
        { m with ClockCounter2 = CounterWithClock.update msg m.ClockCounter2 }

type [<AllowNullLiteral>] AppViewModel (args) =
  inherit ViewModelBase<App2.Model, App2.Msg>(args)
  
  new() = AppViewModel(App2.init () |> ViewModelArgs.simple)

  member _.ClockCounter1 = base.Get() (Binding.SubModelT.req CounterWithClockViewModel >> Binding.mapModel App2.ModelM.ClockCounter1.get >> Binding.mapMsg App2.ClockCounter1Msg)
  member _.ClockCounter2 = base.Get() (Binding.SubModelT.req CounterWithClockViewModel >> Binding.mapModel App2.ModelM.ClockCounter2.get >> Binding.mapMsg App2.ClockCounter2Msg)

module Program =

  let timerTick dispatch =
    let timer = new System.Timers.Timer(1000.)
    timer.Elapsed.Add (fun _ ->
      let clockMsg =
        DateTimeOffset.Now
        |> Clock.Tick
        |> CounterWithClock.ClockMsg
      dispatch <| App2.ClockCounter1Msg clockMsg
      dispatch <| App2.ClockCounter2Msg clockMsg
    )
    timer.Start()


  let main window =

    let logger =
      LoggerConfiguration()
        .MinimumLevel.Override("Elmish.WPF.Update", Events.LogEventLevel.Verbose)
        .MinimumLevel.Override("Elmish.WPF.Bindings", Events.LogEventLevel.Verbose)
        .MinimumLevel.Override("Elmish.WPF.Performance", Events.LogEventLevel.Verbose)
        .WriteTo.Console()
        .CreateLogger()

    WpfProgram.mkSimpleT App2.init App2.update AppViewModel
    |> WpfProgram.withSubscription (fun _ -> Cmd.ofSub timerTick)
    |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))
    |> WpfProgram.startElmishLoop window
