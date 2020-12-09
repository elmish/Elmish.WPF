module Elmish.WPF.Samples.SubModelSeq.List.Program

open System
open Elmish
open Elmish.WPF


type InOutMsg<'a, 'b> =
  | InMsg of 'a
  | OutMsg of 'b


module Option =

  let set a = Option.map (fun _ -> a)


module Func =

  let flip f b a = f a b


let map get set f a =
  a |> get |> f |> Func.flip set a


module List =

  let swap i j =
    List.permute
      (function
        | a when a = i -> j
        | a when a = j -> i
        | a -> a)

  let swapWithNext i = swap i (i + 1)
  let swapWithPrev i = swap i (i - 1)

  let cons head tail = head :: tail

  let mapFirst p f input =
    let rec mapFirstRec reverseFront back =
      match back with
      | [] ->
          (*
           * Conceptually, the correct value to return is
           * reverseFront |> List.rev
           * but this is the same as
           * input
           * so returning that instead.
           *)
          input
      | a :: ma ->
          if p a then
            (reverseFront |> List.rev) @ (f a :: ma)
          else
            mapFirstRec (a :: reverseFront) ma
    mapFirstRec [] input

        
[<AutoOpen>]
module Identifiable =

  type Identifiable<'a> =
    { Id: Guid
      Value: 'a }
  
  module Identifiable =

    let getId m = m.Id
    let get m = m.Value
    let set v m = { m with Value = v }
    let map f = f |> map get set


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
      "CounterValue" |> Binding.oneWay(fun (_, s) -> s.Value.Count)
      "Increment" |> Binding.cmd(Increment |> InMsg)
      "Decrement" |> Binding.cmd(Decrement |> InMsg)
      "StepSize" |> Binding.twoWay(
        (fun (_, s) -> float s.Value.StepSize),
        (fun v _ -> v |> int |> SetStepSize |> InMsg))
      "Reset" |> Binding.cmdIf(
        Reset |> InMsg,
        (fun (_, s) -> canReset s.Value))
    ]


module App =

  type Model =
    { Counters: Identifiable<Counter> list }

  type Msg =
    | CounterMsg of Guid * CounterMsg
    | AddCounter
    | Remove of Guid

  type OutMsg =
    | OutRemove


  let getCounters m = m.Counters
  let setCounters v m = { m with Counters = v }
  let mapCounters f = f |> map getCounters setCounters

  let createNewIdentifiableCounter () =
    { Id = Guid.NewGuid ()
      Value = Counter.init }

  let init () =
    { Counters = [ createNewIdentifiableCounter () ] }

  let hasId id ic = ic.Id = id

  let update = function
    | CounterMsg (cId, msg) -> msg |> Counter.update |> Identifiable.map |> List.mapFirst (fun ic -> ic.Id = cId) |> mapCounters
    | AddCounter -> createNewIdentifiableCounter () |> List.cons |> mapCounters
    | Remove cId -> cId |> hasId >> not |> List.filter |> mapCounters

  let mapOutMsg = function
    | OutRemove -> Remove


module Bindings =

  open App

  let counterBindings () : Binding<Model * Identifiable<Counter>, InOutMsg<CounterMsg, OutMsg>> list =
    [ "CounterIdText" |> Binding.oneWay(fun (_, s) -> s.Id)
      "Remove" |> Binding.cmd(OutRemove |> OutMsg)
    ] @ (Counter.bindings ())

  let rootBindings () : Binding<Model, Msg> list = [
    "Counters" |> Binding.subModelSeq(
      (fun m -> m.Counters),
      (fun ic -> ic.Id),
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
