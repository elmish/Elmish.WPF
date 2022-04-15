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

type [<AllowNullLiteral>] CounterViewModel (args) as this =
  inherit ViewModelBase<Counter.Model,Counter.Msg>(args, fun () -> box this)

  new() = CounterViewModel(Counter.init |> ViewModelArgs.simple)

  member _.StepSize
    with get() = this.getValue (fun m -> m.StepSize)
    and set(v) = this.setValue (fun _m -> Counter.Msg.SetStepSize v)
  member _.CounterValue = this.getValue (fun m -> m.Count)
  member _.Increment = this.cmd((fun _ _ -> Counter.Increment |> ValueSome), (fun _ _ -> true), false)
  member _.Decrement = this.cmd((fun _ _ -> Counter.Decrement |> ValueSome), (fun _ _ -> true), false)
  member _.Reset = this.cmd((fun _ _ -> Counter.Reset |> ValueSome), (fun _ -> Counter.canReset), false)


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

type [<AllowNullLiteral>] ClockViewModel (args) as this =
  inherit ViewModelBase<Clock.Model,Clock.Msg>(args, fun () -> box this)
  
  new() = ClockViewModel(Clock.init () |> ViewModelArgs.simple)

  member _.Time = this.getValue Clock.getTime
  member _.IsLocal = this.getValue (fun m -> m.TimeType = Clock.Local)
  member _.SetLocal = this.cmd ((fun _ _ -> Clock.SetTimeType Clock.Local |> ValueSome), (fun _ _ -> true), false)
  member _.IsUtc = this.getValue (fun m -> m.TimeType = Clock.Utc)
  member _.SetUtc = this.cmd ((fun _ _ -> Clock.SetTimeType Clock.Utc |> ValueSome), (fun _ _ -> true), false)

module CounterWithClock =

  type Model =
    { Counter: Counter.Model
      Clock: Clock.Model
      Id: string }

  let init id =
    { Counter = Counter.init
      Clock = Clock.init ()
      Id = id }

  type Msg =
    | CounterMsg of Counter.Msg
    | ClockMsg of Clock.Msg

  let update msg m =
    match msg with
    | CounterMsg msg -> { m with Counter = Counter.update msg m.Counter }
    | ClockMsg msg -> { m with Clock = Clock.update msg m.Clock }

type [<AllowNullLiteral>] CounterWithClockViewModel (args) as this =
  inherit ViewModelBase<CounterWithClock.Model,CounterWithClock.Msg>(args, fun () -> box this)
  
  new() = CounterWithClockViewModel(CounterWithClock.init "id" |> ViewModelArgs.simple)

  member _.Counter = this.subModel ((fun m -> m.Counter |> ValueSome), (fun _ msg -> CounterWithClock.CounterMsg msg), CounterViewModel)
  member _.Clock = this.subModel ((fun m -> m.Clock |> ValueSome), (fun _ msg -> CounterWithClock.ClockMsg msg), ClockViewModel)
  member _.Id = this.getValue (fun m -> m.Id)

module App2 =

  type Model =
    { ClockCounters: CounterWithClock.Model seq }

  let init () =
    { ClockCounters = CounterWithClock.init |> Seq.replicate 4 |> Seq.mapi (fun i x -> i |> string |> x) }

  type Msg =
    | ClockCountersMsg of string * CounterWithClock.Msg
    | AllClockCountersMsg of CounterWithClock.Msg
    | AddClockCounter
    | RemoveClockCounter of string

  let update msg m =
    match msg with
    | ClockCountersMsg (id, msg) ->
        { m with ClockCounters = m.ClockCounters |> Seq.map (fun m -> if id = m.Id then CounterWithClock.update msg m else m) }
    | AllClockCountersMsg msg ->
        { m with ClockCounters = m.ClockCounters |> Seq.map (CounterWithClock.update msg) }
    | AddClockCounter ->
        { m with ClockCounters = Seq.append m.ClockCounters [ CounterWithClock.init (m.ClockCounters |> Seq.map (fun c -> c.Id |> int) |> Seq.max |> (+) 1 |> string) ] }
    | RemoveClockCounter id ->
        { m with ClockCounters = m.ClockCounters |> Seq.where (fun m -> id <> m.Id) }

type [<AllowNullLiteral>] AppViewModel (args) as this =
  inherit ViewModelBase<App2.Model,App2.Msg>(args, fun () -> box this)
  
  new() = AppViewModel(App2.init () |> ViewModelArgs.simple)

  member _.ClockCounters = this.subModelSeqKeyed ((fun m -> m.ClockCounters), (fun m -> m.Id), (fun _ msg -> App2.ClockCountersMsg msg), CounterWithClockViewModel)
  member _.AddClockCounter = this.cmd ((fun _ _ -> App2.AddClockCounter |> ValueSome), (fun _ _ -> true), true)
  member _.RemoveClockCounter = this.cmd ((fun p _ -> p |> unbox |> App2.RemoveClockCounter |> ValueSome), (fun bi _ -> bi <> null), true)

module Program =

  let timerTick dispatch =
    let timer = new System.Timers.Timer(1000.)
    timer.Elapsed.Add (fun _ ->
      let clockMsg =
        DateTimeOffset.Now
        |> Clock.Tick
        |> CounterWithClock.ClockMsg
      dispatch <| App2.AllClockCountersMsg clockMsg
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

    WpfProgram.mkSimpleBase App2.init App2.update AppViewModel
    |> WpfProgram.withSubscription (fun _ -> Cmd.ofSub timerTick)
    |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))
    |> WpfProgram.startElmishLoop window
