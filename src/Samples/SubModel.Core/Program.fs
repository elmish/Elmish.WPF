namespace Elmish.WPF.Samples.SubModel

open System
open Serilog
open Serilog.Extensions.Logging
open Elmish
open Elmish.WPF
open System.Runtime.CompilerServices


type ViewModelBase2<'model, 'msg>() =
  let mutable _bindings: Binding<'model, 'msg> list = []

  member public _.Bindings
    with get() = _bindings

  member _.bind(memberName: string, b: string -> Binding<'model, 'msg>, designValue: 'b) =
    //let memberName = Option.defaultValue "" memberName
    let binding = b memberName
    _bindings <- binding::_bindings
    designValue

  member this.oneWay(x: 'model -> 'b, designValue: 'b, [<CallerMemberName>] ?memberName: string) =
    let memberName = Option.defaultValue "" memberName
    let binding = memberName |> Binding.oneWay(x)
    _bindings <- binding::_bindings
    designValue

  member _.cmd(x: 'msg, [<CallerMemberName>] ?memberName: string) =
    let memberName = Option.defaultValue "" memberName
    let binding = memberName |> Binding.cmd(x)
    _bindings <- binding::_bindings
    ()

  member _.cmdIf(x: 'msg, canExec: 'model -> bool, [<CallerMemberName>] ?memberName: string) =
    let memberName = Option.defaultValue "" memberName
    let binding = memberName |> Binding.cmdIf(x, canExec)
    _bindings <- binding::_bindings
    ()

  member _.twoWay(x: 'model -> 'b, y: 'b -> 'msg, designValue: 'b, [<CallerMemberName>] ?memberName: string) =
    let memberName = Option.defaultValue "" memberName
    let binding = memberName |> Binding.twoWay(x,y)
    _bindings <- binding::_bindings
    designValue

  member _.subModel(getSubModel: 'model -> 'subModel, toBindingModel: 'model * 'subModel -> 'bindingModel, toMsg: 'bindingMsg -> 'msg, (viewModel: #ViewModelBase2<'bindingModel,'bindingMsg>), [<CallerMemberName>] ?memberName: string) =
    let memberName = Option.defaultValue "" memberName
    let binding = memberName |> (Binding.SubModel.required (fun () -> viewModel.Bindings)
      >> Binding.mapModel (fun m -> toBindingModel (m, getSubModel m))
      >> Binding.mapMsg toMsg)
    //let binding = memberName |> Binding.subModel(getSubModel, toBindingModel, toMsg, (fun () -> viewModel.Bindings))
    _bindings <- binding::_bindings
    viewModel

  member _.subModel2(getSubModel: 'model -> 'subModel, toBindingModel: 'model * 'subModel -> 'bindingModel, toMsg: 'bindingMsg -> 'msg, (createVm: 'bindingModel * ('bindingMsg -> unit) -> 'viewModel when 'viewModel :> IViewModel<'bindingModel>), [<CallerMemberName>] ?memberName: string) =
    let memberName = Option.defaultValue "" memberName
    let binding = memberName |> (Binding.SubModelVm.required (createVm)
      >> Binding.mapModel (fun m -> toBindingModel (m, getSubModel m))
      >> Binding.mapMsg toMsg)
    //let binding = memberName |> Binding.subModel(getSubModel, toBindingModel, toMsg, (fun () -> viewModel.Bindings))
    _bindings <- binding::_bindings
    null

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

  let bindings () : Binding<Model, Msg> list = [
    "CounterValue" |> Binding.oneWay (fun m -> m.Count)
    "Increment" |> Binding.cmd Increment
    "Decrement" |> Binding.cmd Decrement
    "StepSize" |> Binding.twoWay(
      (fun m -> float m.StepSize),
      int >> SetStepSize)
    "Reset" |> Binding.cmdIf(Reset, canReset)
  ]


type [<AllowNullLiteral>] public CounterViewModel(initialModel, dispatch) as this =
  inherit ViewModelBase<Counter.Model, Counter.Msg>(initialModel, dispatch)

  new() = CounterViewModel(Counter.init, ignore)

  member _.StepSize
    with get() = this.getValue (fun m -> m.StepSize)
    and set(v) = this.setValue ((fun v _m -> Counter.Msg.SetStepSize v), v)
  member _.CounterValue = this.getValue (fun m -> m.Count)
  member _.Increment = this.cmd((fun _ _ -> Counter.Increment |> ValueSome), (fun _ _ -> true))
  member _.Decrement = this.cmd((fun _ _ -> Counter.Decrement |> ValueSome), (fun _ _ -> true))
  member _.Reset = this.cmd((fun _ _ -> Counter.Reset |> ValueSome), (fun _ -> Counter.canReset))

type public CounterViewModel2() as this =
  inherit ViewModelBase2<Counter.Model, Counter.Msg>()

  let counterValueBinding = this.oneWay ((fun m -> m.Count), 3, nameof this.CounterValue)
  let incrementBinding = this.cmd (Counter.Increment, nameof this.Increment)
  let decrementBinding = this.cmd (Counter.Decrement, nameof this.Decrement)
  let stepSizeBinding = this.twoWay((fun m -> float m.StepSize), int >> Counter.SetStepSize, 1.0, nameof this.StepSize)
  let resetBinding = this.cmdIf(Counter.Reset, Counter.canReset, nameof this.Reset)
  member _.CounterValue = counterValueBinding
  member _.Increment = incrementBinding
  member _.Decrement = decrementBinding
  member _.StepSize
    with get() = stepSizeBinding
    and set(_:float) = ()
  member _.Reset = resetBinding

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

  let bindings () : Binding<Model, Msg> list = [
    "Time" |> Binding.oneWay getTime
    "IsLocal" |> Binding.oneWay (fun m -> m.TimeType = Local)
    "SetLocal" |> Binding.cmd (SetTimeType Local)
    "IsUtc" |> Binding.oneWay (fun m -> m.TimeType = Utc)
    "SetUtc" |> Binding.cmd (SetTimeType Utc)
  ]

  
type [<AllowNullLiteral>] public ClockViewModel(initialModel, dispatch) as this =
  inherit ViewModelBase<Clock.Model, Clock.Msg>(initialModel, dispatch)
  
  new() = ClockViewModel(Clock.init (), ignore)

  member _.Time = this.getValue (fun m -> m.Time)
  member _.IsLocal = this.getValue (fun m -> m.TimeType = Clock.Local)
  member _.SetLocal = this.cmd ((fun _ _ -> Clock.SetTimeType Clock.Local |> ValueSome), (fun _ _ -> true))
  member _.IsUtc = this.getValue (fun m -> m.TimeType = Clock.Utc)
  member _.SetUtc = this.cmd ((fun _ _ -> Clock.SetTimeType Clock.Utc |> ValueSome), (fun _ _ -> true))


type public ClockViewModel2() as this =
  inherit ViewModelBase2<Clock.Model, Clock.Msg>()

  let timeBinding = this.oneWay (Clock.getTime, DateTime.Now, nameof this.Time)
  let isLocalBinding = this.oneWay ((fun m -> m.TimeType = Clock.Local), true, nameof this.IsLocal)
  let setLocalBinding = this.cmd (Clock.SetTimeType Clock.Local, nameof this.SetLocal)
  let isUtcBinding = this.oneWay ((fun m -> m.TimeType = Clock.Utc), false, nameof this.IsUtc)
  let setUtcBinding = this.cmd (Clock.SetTimeType Clock.Utc, nameof this.SetUtc)
  member _.Time = timeBinding
  member _.IsLocal = isLocalBinding
  member _.SetLocal = setLocalBinding
  member _.IsUtc = isUtcBinding
  member _.SetUtc = setUtcBinding


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

  let bindings () : Binding<Model, Msg> list = [
    "Counter"
      |> Binding.SubModel.required Counter.bindings
      |> Binding.mapModel (fun m -> m.Counter)
      |> Binding.mapMsg CounterMsg
    "Clock"
      |> Binding.SubModel.required Clock.bindings
      |> Binding.mapModel (fun m -> m.Clock)
      |> Binding.mapMsg ClockMsg
  ]
  
type [<AllowNullLiteral>] public CounterWithClockViewModel(initialModel, dispatch) as this =
  inherit ViewModelBase<CounterWithClock.Model, CounterWithClock.Msg>(initialModel, dispatch)

  new() = CounterWithClockViewModel(CounterWithClock.init (), ignore)

  member _.Counter = this.subModel((fun m -> m.Counter |> ValueSome), (fun _ -> CounterWithClock.Msg.CounterMsg), CounterViewModel)
  member _.Clock = this.subModel((fun m -> m.Clock |> ValueSome), (fun _ -> CounterWithClock.Msg.ClockMsg), ClockViewModel)


type public CounterWithClockViewModel2() as this =
  inherit ViewModelBase2<CounterWithClock.Model, CounterWithClock.Msg>()
  let counterBinding = this.subModel2((fun m -> m.Counter), snd, CounterWithClock.Msg.CounterMsg, (fun (m,d) -> CounterViewModel (m,d) :> IViewModel<_>), nameof this.Counter)
  let clockBinding = this.subModel((fun m -> m.Clock), snd, CounterWithClock.Msg.ClockMsg, ClockViewModel2(), nameof this.Clock)

  member _.Counter = counterBinding
  member _.Clock = clockBinding


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

  let bindings () : Binding<Model, Msg> list = [
    "ClockCounter1"
      |> Binding.SubModel.required CounterWithClock.bindings
      |> Binding.mapModel (fun m -> m.ClockCounter1)
      |> Binding.mapMsg ClockCounter1Msg

    "ClockCounter2"
      |> Binding.SubModel.required CounterWithClock.bindings
      |> Binding.mapModel (fun m -> m.ClockCounter2)
      |> Binding.mapMsg ClockCounter2Msg
  ]
  
type public MainViewModel(initialModel, dispatch) as this =
  inherit ViewModelBase<App2.Model, App2.Msg>(initialModel, dispatch)

  new() = MainViewModel(App2.init (), ignore)

  member _.ClockCounter1 = this.subModel((fun m -> m.ClockCounter1 |> ValueSome), (fun _ -> App2.Msg.ClockCounter1Msg), CounterWithClockViewModel)
  member _.ClockCounter2 = this.subModel((fun m -> m.ClockCounter2 |> ValueSome), (fun _ -> App2.Msg.ClockCounter2Msg), CounterWithClockViewModel)

type public MainViewModel2() as this =
  inherit ViewModelBase2<App2.Model, App2.Msg>()
  let clockCounter1Binding = this.subModel((fun m -> m.ClockCounter1), snd, App2.Msg.ClockCounter1Msg, CounterWithClockViewModel2(), nameof this.ClockCounter1)
  let clockCounter2Binding = this.subModel((fun m -> m.ClockCounter2), snd, App2.Msg.ClockCounter2Msg, CounterWithClockViewModel2(), nameof this.ClockCounter2)

  member _.ClockCounter1 = clockCounter1Binding
  member _.ClockCounter2 = clockCounter2Binding

module Program =
  let counterDesignVm = ViewModel.designInstance Counter.init (Counter.bindings ())
  let clockDesignVm = ViewModel.designInstance (Clock.init ()) (Clock.bindings ())
  let counterWithClockDesignVm = ViewModel.designInstance (CounterWithClock.init ()) (CounterWithClock.bindings ())
  let mainDesignVm = ViewModel.designInstance (App2.init ()) (App2.bindings ())


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

    WpfProgram.mkSimple2 App2.init App2.update MainViewModel
    |> WpfProgram.withSubscription (fun _ -> Cmd.ofSub timerTick)
    |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))
    |> WpfProgram.startElmishLoop window
