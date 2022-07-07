namespace Elmish.WPF.Samples.SubModelStatic

open System
open Serilog
open Serilog.Extensions.Logging
open Elmish
open Elmish.WPF
open System.ComponentModel

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

open Counter
type [<AllowNullLiteral>] CounterViewModel (args) as this =
  let sh = StaticHelper.create args (fun () -> box this)

  new() = CounterViewModel(init |> ViewModelArgs.simple)

  member _.StepSize
    with get() =
      BindingT.Get.get (fun m -> m.StepSize)
      |> sh.Get ()
    and set(v) =
      BindingT.Set.id
      >> BindingT.mapMsg (fun msg -> Msg.SetStepSize msg)
      |> sh.Set (v)

  member _.CounterValue =
    BindingT.Get.get (fun m -> m.Count)
    |> sh.Get ()
  member _.Increment =
    BindingT.Cmd.createWithParam (fun _ _ -> Increment |> ValueSome) (fun _ _ -> true) true
    |> sh.Get ()
  member _.Decrement =
    BindingT.Cmd.createWithParam (fun _ _ -> Decrement |> ValueSome) (fun _ _ -> true) true
    |> sh.Get ()
  member _.Reset =
    BindingT.Cmd.createWithParam (fun _ _ -> Reset |> ValueSome) (fun _ -> canReset) true
    |> sh.Get ()

  interface ISubModel<Model, Msg> with
    member _.StaticHelper = sh

  interface INotifyPropertyChanged with
    [<CLIEvent>]
    member this.PropertyChanged = (sh :> INotifyPropertyChanged).PropertyChanged


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

open Clock
type [<AllowNullLiteral>] ClockViewModel (args) as this =
  let sh = StaticHelper.create args (fun () -> box this)
  
  new() = ClockViewModel(init () |> ViewModelArgs.simple)

  member _.Time =
    BindingT.Get.get getTime
    |> sh.Get ()
  member _.IsLocal =
    BindingT.Get.id
    >> BindingT.mapModel (fun m -> m.TimeType = Clock.Local)
    |> sh.Get()
  member _.SetLocal =
    BindingT.Cmd.createWithParam (fun _ _ -> Clock.SetTimeType Clock.Local |> ValueSome) (fun _ _ -> true) true
    |> sh.Get ()
  member _.IsUtc =
    BindingT.Get.id
    >> BindingT.mapModel (fun m -> m.TimeType = Clock.Utc)
    |> sh.Get ()
  member _.SetUtc =
    BindingT.Cmd.createWithParam (fun _ _ -> Clock.SetTimeType Clock.Utc |> ValueSome) (fun _ _ -> true) true
    |> sh.Get ()

  interface ISubModel<Model, Msg> with
    member _.StaticHelper = sh

  interface INotifyPropertyChanged with
    [<CLIEvent>]
    member this.PropertyChanged = (sh :> INotifyPropertyChanged).PropertyChanged

module CounterWithClock =

  type Model =
    { Counter: Counter.Model
      Clock: Clock.Model }

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

open CounterWithClock
type [<AllowNullLiteral>] CounterWithClockViewModel (args) as this =
  let sh = StaticHelper.create args (fun () -> box this)
  
  new() = CounterWithClockViewModel(init () |> ViewModelArgs.simple)

  member _.Counter =
    BindingT.SubModel.req CounterViewModel
    >> BindingT.mapModel (fun m -> m.Counter)
    >> BindingT.mapMsg CounterMsg
    |> sh.Get()
  member _.Clock =
    BindingT.SubModel.req ClockViewModel
    >> BindingT.mapModel (fun m -> m.Clock)
    >> BindingT.mapMsg ClockMsg
    |> sh.Get()

  interface ISubModel<Model, Msg> with
    member _.StaticHelper = sh

  interface INotifyPropertyChanged with
    [<CLIEvent>]
    member _.PropertyChanged = (sh :> INotifyPropertyChanged).PropertyChanged

module App2 =

  type Model =
    { ClockCounter1: CounterWithClock.Model
      ClockCounter2: CounterWithClock.Model }

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

open App2
type [<AllowNullLiteral>] AppViewModel (args) as this =
  let sh = StaticHelper.create args (fun () -> box this)

  new() = AppViewModel(init () |> ViewModelArgs.simple)

  member _.ClockCounter1 =
    BindingT.SubModel.req CounterWithClockViewModel
    >> BindingT.mapModel (fun m -> m.ClockCounter1)
    >> BindingT.mapMsg ClockCounter1Msg
    |> sh.Get ()
  member _.ClockCounter2 =
    BindingT.SubModel.req CounterWithClockViewModel
    >> BindingT.mapModel (fun m -> m.ClockCounter2)
    >> BindingT.mapMsg ClockCounter2Msg
    |> sh.Get ()

  interface ISubModel<Model, Msg> with
    member _.StaticHelper = sh

  interface INotifyPropertyChanged with
    [<CLIEvent>]
    member this.PropertyChanged = (sh :> INotifyPropertyChanged).PropertyChanged

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
