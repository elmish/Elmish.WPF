﻿module Elmish.WPF.Samples.UiBoundCmdParam.Program

open System
open Elmish
open Elmish.WPF


type Model =
  { Numbers: int list
    EnabledMaxLimit: int }

let init () =
  { Numbers = [0 .. 10]
    EnabledMaxLimit = 5 }

type Msg =
  | SetLimit of int
  | Command

let update msg m =
  match msg with
  | SetLimit x -> { m with EnabledMaxLimit = x }
  | Command -> m

let bindings () : Binding<Model, Msg> list = [
  "Numbers" |> Binding.oneWay(fun m -> m.Numbers)
  "Limit" |> Binding.twoWay((fun m -> float m.EnabledMaxLimit), int >> SetLimit)
  "Command" |> Binding.cmdParamIf(
    (function
      | :? int as i -> Some i
      | _ -> None),
    (fun _ _ -> Command),
    (fun oi m -> oi |> Option.map (fun i -> i <= m.EnabledMaxLimit) |> Option.defaultValue false),
    true)
]


[<EntryPoint; STAThread>]
let main _ =
  Program.mkSimpleWpf init update bindings
  |> Program.withConsoleTrace
  |> Program.runWindowWithConfig
    { ElmConfig.Default with LogConsole = true; Measure = true }
    (MainWindow())
