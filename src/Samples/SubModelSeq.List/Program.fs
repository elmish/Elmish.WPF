module Elmish.WPF.Samples.SubModelSeq.List.Program

open Elmish
open Elmish.WPF


type InOutMsg<'a, 'b> =
  | InMsg of 'a
  | OutMsg of 'b


module List =

  let cons head tail = head :: tail

  let mapAtIndex idx f =
    List.indexed >> List.map (fun (i, a) -> if i = idx then f a else a)

  let removeAtIndex idx ma =
    [ ma |> List.take idx
      ma |> List.skip (idx + 1)
    ] |> List.concat


[<AutoOpen>]
module Counter =

  type Counter =
    { Count: int
      StepSize: int }

  type CounterMsg =
    | Increment
    | Decrement
    | SetStepSize of int
    | Reset

  module Counter =

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

    let bindings () = [
      "CounterValue" |> Binding.oneWay(fun (_, _, c) -> c.Count)
      "Increment" |> Binding.cmd(Increment |> InMsg)
      "Decrement" |> Binding.cmd(Decrement |> InMsg)
      "StepSize" |> Binding.twoWay(
        (fun (_, _, c) -> float c.StepSize),
        (fun v _ -> v |> int |> SetStepSize |> InMsg))
      "Reset" |> Binding.cmdIf(
        Reset |> InMsg,
        (fun (_, _, c) -> canReset c))
    ]


module App =

  type Model = Counter list

  type Msg =
    | CounterMsg of int * CounterMsg
    | AddCounter
    | Remove of int

  type OutMsg =
    | OutRemove


  let init () =
    [ Counter.init ]

  let update = function
    | CounterMsg (idx, msg) -> msg |> Counter.update |> List.mapAtIndex idx
    | AddCounter -> Counter.init |> List.cons
    | Remove idx -> idx |> List.removeAtIndex

  let mapOutMsg = function
    | OutRemove -> Remove


module Bindings =

  open App

  let counterBindings () : Binding<Model * int * Counter, InOutMsg<CounterMsg, OutMsg>> list =
    [ "Remove" |> Binding.cmd(OutRemove |> OutMsg)
    ] @ (Counter.bindings ())

  let rootBindings () : Binding<Model, Msg> list = [
    "Counters" |> Binding.subModelSeq(
      (fun m -> m |> List.indexed),
      (fun (m, (idx, c)) -> (m, idx, c)),
      (fun (_, idx, _) -> idx),
      (fun (cId, inOutMsg) ->
        match inOutMsg with
        | InMsg msg -> (cId, msg) |> CounterMsg
        | OutMsg msg -> cId |> mapOutMsg msg),
      counterBindings)

    "AddCounter" |> Binding.cmd AddCounter
  ]


let mainDesignVm = ViewModel.designInstance (App.init ()) (Bindings.rootBindings ())

let main window =
  Program.mkSimpleWpf App.init App.update Bindings.rootBindings
  |> Program.withConsoleTrace
  |> Program.runWindowWithConfig
    { ElmConfig.Default with LogConsole = true; Measure = true }
    window
