module Elmish.WPF.Samples.SubModel.Program

open System
open Elmish
open Elmish.WPF


module Clock =

  type Model =
    { Time: DateTimeOffset
      UseUtc: bool }

  let init () =
    { Time = DateTimeOffset.Now
      UseUtc = false }

  type Msg =
    | Tick of DateTimeOffset
    | ToggleUtc

  let update msg m =
    match msg with
    | Tick t -> { m with Time = t }
    | ToggleUtc -> { m with UseUtc = not m.UseUtc }

  let bindings () =
    [ 
      "Time" |> Binding.oneWay 
        (fun m -> if m.UseUtc then m.Time.UtcDateTime else m.Time.LocalDateTime)
      "ToggleUtc" |> Binding.cmd (fun m -> ToggleUtc)
    ]


module CounterWithClock =

  type Model =
    { Count: int
      StepSize: int
      Clock: Clock.Model }

  let init () =
    { Count = 0
      StepSize = 1
      Clock = Clock.init () }

  type Msg =
    | Increment
    | Decrement
    | SetStepSize of int
    | Reset
    | ClockMsg of Clock.Msg

  let update msg m =
    match msg with
    | Increment -> { m with Count = m.Count + m.StepSize }
    | Decrement -> { m with Count = m.Count - m.StepSize }
    | SetStepSize x -> { m with StepSize = x }
    | Reset -> { m with Count = 0; StepSize = 1 }
    | ClockMsg msg -> { m with Clock = Clock.update msg m.Clock }

  let bindings () =
    [
      "CounterValue" |> Binding.oneWay (fun m -> m.Count)
      "Increment" |> Binding.cmd (fun m -> Increment)
      "Decrement" |> Binding.cmd (fun m -> Decrement)
      "StepSize" |> Binding.twoWay 
        (fun m -> float m.StepSize)
        (fun v m -> int v |> SetStepSize)
      "Reset" |> Binding.cmdIf
        (fun m -> Reset)
        (fun m ->
          let i = init ()
          m.Count <> i.Count || m.StepSize <> i.StepSize
        )
      "Clock" |> Binding.subModel (fun m -> m.Clock) Clock.bindings ClockMsg
    ]


module App =

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

  let bindings model dispatch =
    [
      "ClockCounter1" |> Binding.subModel
        (fun m -> m.ClockCounter1) CounterWithClock.bindings ClockCounter1Msg
      "ClockCounter2" |> Binding.subModel
        (fun m -> m.ClockCounter2) CounterWithClock.bindings ClockCounter2Msg
    ]


let timerTick dispatch =
  let timer = new System.Timers.Timer(1000.)
  timer.Elapsed.Add (fun _ -> 
    let clockMsg =
      DateTimeOffset.Now
      |> Clock.Tick
      |> CounterWithClock.ClockMsg
    dispatch <| App.ClockCounter1Msg clockMsg
    dispatch <| App.ClockCounter2Msg clockMsg
  )
  timer.Start()


[<EntryPoint; STAThread>]
let main argv =
  Program.mkSimple App.init App.update App.bindings
  |> Program.withSubscription (fun m -> Cmd.ofSub timerTick)
  |> Program.withConsoleTrace
  |> Program.runWindowWithConfig
      { ElmConfig.Default with LogConsole = true }
      (MainWindow())
