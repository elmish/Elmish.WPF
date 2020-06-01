module Elmish.WPF.Samples.OneWaySeq.Program

open System
open Elmish
open Elmish.WPF


type Model =
  { OneWaySeqNumbers: int list
    OneWayNumbers: int list }

let init () =
  { OneWaySeqNumbers = [ 1000..-1..1 ]
    OneWayNumbers = [ 1000..-1..1 ] }

type Msg =
  | AddOneWaySeqNumber
  | AddOneWayNumber

let update msg m =
  match msg with
  | AddOneWaySeqNumber -> { m with OneWaySeqNumbers = m.OneWaySeqNumbers.Head + 1 :: m.OneWaySeqNumbers }
  | AddOneWayNumber -> { m with OneWayNumbers = m.OneWayNumbers.Head + 1 :: m.OneWayNumbers }

let bindings () : Binding<Model, Msg> list = [
  "OneWaySeqNumbers" |> Binding.oneWaySeq((fun m -> m.OneWaySeqNumbers), (=), id)
  "OneWayNumbers" |> Binding.oneWay (fun m -> m.OneWayNumbers)
  "AddOneWaySeqNumber" |> Binding.cmd AddOneWaySeqNumber
  "AddOneWayNumber" |> Binding.cmd AddOneWayNumber
]


[<EntryPoint; STAThread>]
let main _ =
  Program.mkSimpleWpf init update bindings
  |> ElmishProgram.withConsoleTrace
  |> Program.logConsole
  |> Program.measure
  |> Program.runWindow
    (MainWindow())
