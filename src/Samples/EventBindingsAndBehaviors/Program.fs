module Elmish.WPF.Samples.EventBindingsAndBehaviors.Program

open System
open Elmish
open Elmish.WPF


type Model =
  { Msg1: string
    Msg2: string }

let init () =
  { Msg1 = ""
    Msg2 = "" }

type Msg =
  | GotFocus1
  | GotFocus2
  | LostFocus1
  | LostFocus2

let update msg m =
  match msg with
  | GotFocus1 -> { m with Msg1 = "Focused" }
  | GotFocus2 -> { m with Msg2 = "Focused" }
  | LostFocus1 -> { m with Msg1 = "Not focused" }
  | LostFocus2 -> { m with Msg2 = "Not focused" }

let bindings () : Binding<Model, Msg> list = [
  "Msg1" |> Binding.oneWay (fun m -> m.Msg1)
  "Msg2" |> Binding.oneWay (fun m -> m.Msg2)
  "GotFocus1" |> Binding.cmd GotFocus1
  "GotFocus2" |> Binding.cmd GotFocus2
  "LostFocus1" |> Binding.cmd LostFocus1
  "LostFocus2" |> Binding.cmd LostFocus2
]


[<EntryPoint; STAThread>]
let main argv =
  Program.mkSimpleWpf init update bindings
  |> Program.withConsoleTrace
  |> Program.runWindowWithConfig
      { ElmConfig.Default with LogConsole = true; Measure = true }
      (MainWindow())
