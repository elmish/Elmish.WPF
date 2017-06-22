namespace Elmish.Counter

open System
open Elmish
open Elmish.WPF

module Types =

    type ClockMsg =
        | Tick of DateTime

    type ClockModel =
        { Time: DateTime }

    type Msg =
        | ClockMsg of ClockMsg
        | Increment
        | Decrement
        | SetStepSize of int
        | SetToSource of string
        | SetTwoWay of string

    type Model = 
        { Count: int
          StepSize: int
          Clock: ClockModel
          ToSource: string
          TwoWay: string }


module State =
    open Types
     
    let init() = { Count = 0; StepSize = 1; Clock = { Time = DateTime.Now }; ToSource = ""; TwoWay = "Default" }

    let timerTick dispatch =
        let timer = new System.Timers.Timer 1.
        timer.Elapsed.Subscribe (fun _ -> dispatch (System.DateTime.Now |> Tick |> ClockMsg)) |> ignore
        //timer.Enabled <- true
        //timer.Start()

    let subscribe model =
        Cmd.ofSub timerTick

    let clockUpdate (msg:ClockMsg) (model:ClockModel) =
        match msg with
        | Tick t -> { model with Time = t }

    let update (msg:Msg) (model:Model) =
        console.log <| sprintf "Updating %A" msg
        match msg with
        | Increment -> { model with Count = model.Count + model.StepSize }
        | Decrement -> { model with Count = model.Count - model.StepSize }
        | SetStepSize n -> { model with StepSize = n }
        | ClockMsg m -> { model with Clock = clockUpdate m model.Clock }
        | SetToSource s -> { model with ToSource = s }
        | SetTwoWay s -> { model with TwoWay = s }
        

module App =
    open State
    open Types

    let view _ _ = 
        let clockViewBinding : ViewBindings<ClockModel,ClockMsg> =
            [ "Time" |> Binding.oneWay (fun m -> m.Time) ]

        [ "Increment" |> Binding.cmd (fun m -> Increment)
          "Decrement" |> Binding.cmdIf (fun m -> Decrement) (fun m -> m.StepSize = 1)
          "Count" |> Binding.oneWay (fun m -> m.Count)
          "StepSize" |> Binding.twoWay (fun m -> (double m.StepSize)) (fun v m -> v |> int |> SetStepSize)
          "Clock" |> Binding.vm (fun m -> m.Clock) clockViewBinding ClockMsg
          "ToSource" |> Binding.twoWay (fun m -> m.ToSource) (fun v m -> SetToSource v)
          "TwoWay" |> Binding.twoWay (fun m -> m.TwoWay) (fun v m -> SetTwoWay v) ]

    [<EntryPoint;STAThread>]
    let main argv = 
        Program.mkSimple init update view
        //|> Program.withConsoleTrace
        |> Program.withSubscription subscribe
        |> Program.runWindow (Elmish.CounterViews.MainWindow())
