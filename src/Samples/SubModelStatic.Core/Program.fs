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
  let sh = StaticHelper.create args (fun () -> box this)

  new() = CounterViewModel(Counter.init |> ViewModelArgs.simple)

  member _.StepSize
    with get() = sh.Get () (BindingT.Get.id >> BindingT.mapModel (fun m -> m.StepSize))
    and set(v) = sh.Set (v) (BindingT.Set.id >> BindingT.mapMsg Counter.Msg.SetStepSize)
  member _.CounterValue = sh.Get () (BindingT.Get.id >> BindingT.mapModel (fun m -> m.Count))
  member _.Increment = sh.Get () (BindingT.Cmd.createWithParam (fun _ _ -> Counter.Increment |> ValueSome) (fun _ _ -> true) true)
  member _.Decrement = sh.Get () (BindingT.Cmd.createWithParam (fun _ _ -> Counter.Decrement |> ValueSome) (fun _ _ -> true) true)
  member _.Reset = sh.Get () (BindingT.Cmd.createWithParam (fun _ _ -> Counter.Reset |> ValueSome) (fun _ -> Counter.canReset) true)

  interface ISubModel<Counter.Model, Counter.Msg> with
    member _.StaticHelper = sh


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
  let sh = StaticHelper.create args (fun () -> box this)
  
  new() = ClockViewModel(Clock.init () |> ViewModelArgs.simple)

  member _.Time = sh.Get() (BindingT.Get.id >> BindingT.mapModel Clock.getTime)
  member _.IsLocal = sh.Get() (BindingT.Get.id >> BindingT.mapModel (fun m -> m.TimeType = Clock.Local))
  member _.SetLocal = sh.Get () (BindingT.Cmd.createWithParam (fun _ _ -> Clock.SetTimeType Clock.Local |> ValueSome) (fun _ _ -> true) true)
  member _.IsUtc = sh.Get() (BindingT.Get.id >> BindingT.mapModel (fun m -> m.TimeType = Clock.Utc))
  member _.SetUtc = sh.Get () (BindingT.Cmd.createWithParam (fun _ _ -> Clock.SetTimeType Clock.Utc |> ValueSome) (fun _ _ -> true) true)

  interface ISubModel<Clock.Model, Clock.Msg> with
    member _.StaticHelper = sh

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

type [<AllowNullLiteral>] CounterWithClockViewModel (args) as this =
  let sh = StaticHelper.create args (fun () -> box this)
  
  new() = CounterWithClockViewModel(CounterWithClock.init () |> ViewModelArgs.simple)

  member _.Counter = sh.Get() (BindingT.SubModel.id CounterViewModel >> BindingT.mapModel CounterWithClock.ModelM.Counter.get >> BindingT.mapMsg CounterWithClock.CounterMsg)
  member _.Clock = sh.Get() (BindingT.SubModel.id ClockViewModel >> BindingT.mapModel CounterWithClock.ModelM.Clock.get >> BindingT.mapMsg CounterWithClock.ClockMsg)

  interface ISubModel<CounterWithClock.Model, CounterWithClock.Msg> with
    member _.StaticHelper = sh

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

type [<AllowNullLiteral>] AppViewModel (args) as this =
  let sh = StaticHelper.create args (fun () -> box this)
  
  new() = AppViewModel(App2.init () |> ViewModelArgs.simple)

  member _.ClockCounter1 = sh.Get() (BindingT.SubModel.id CounterWithClockViewModel >> BindingT.mapModel App2.ModelM.ClockCounter1.get >> BindingT.mapMsg App2.ClockCounter1Msg)
  member _.ClockCounter2 = sh.Get() (BindingT.SubModel.id CounterWithClockViewModel >> BindingT.mapModel App2.ModelM.ClockCounter2.get >> BindingT.mapMsg App2.ClockCounter2Msg)

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

    WpfProgram.mkSimpleBase App2.init App2.update AppViewModel
    |> WpfProgram.withSubscription (fun _ -> Cmd.ofSub timerTick)
    |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))
    |> WpfProgram.startElmishLoop window
