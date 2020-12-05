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


module FuncOption =

  let inputIfNone f a = a |> f |> Option.defaultValue a
  
  let map (f: 'b -> 'c) (mb: 'a -> 'b option) =
    mb >> Option.map f

  let bind (f: 'b -> 'a -> 'c) (mb: 'a -> 'b option) a =
    mb a |> Option.bind (fun b -> Some(f b a))


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


module App =

  type Model =
    { SomeGlobalState: bool
      Counters: Identifiable<Counter> list }

  type Msg =
    | ToggleGlobalState
    | CounterMsg of Guid * CounterMsg
    | AddCounter
    | Remove of Guid
    | MoveUp of Guid
    | MoveDown of Guid

  type OutMsg =
    | OutRemove
    | OutMoveUp
    | OutMoveDown


  let getSomeGlobalState m = m.SomeGlobalState
  let setSomeGlobalState v m = { m with SomeGlobalState = v }
  let mapSomeGlobalState f = f |> map getSomeGlobalState setSomeGlobalState

  let getCounters m = m.Counters
  let setCounters v m = { m with Counters = v }
  let mapCounters f = f |> map getCounters setCounters

  let createNewIdentifiableCounter () =
    { Id = Guid.NewGuid ()
      Value = Counter.init }

  let init () =
    { SomeGlobalState = false
      Counters = [ createNewIdentifiableCounter () ] }

  let hasId id ic = ic.Id = id

  let swapCounters swap nId =
    nId
    |> hasId
    |> List.tryFindIndex
    |> FuncOption.bind swap
    |> FuncOption.inputIfNone

  let update = function
    | ToggleGlobalState -> mapSomeGlobalState not
    | CounterMsg (cId, msg) -> msg |> Counter.update |> Identifiable.map |> List.mapFirst (fun ic -> ic.Id = cId) |> mapCounters
    | AddCounter -> createNewIdentifiableCounter () |> List.cons |> mapCounters
    | Remove cId -> cId |> hasId >> not |> List.filter |> mapCounters
    | MoveUp cId -> cId |> swapCounters List.swapWithPrev |> mapCounters
    | MoveDown cId -> cId |> swapCounters List.swapWithNext |> mapCounters

  let mapOutMsg = function
    | OutRemove -> Remove
    | OutMoveUp -> MoveUp
    | OutMoveDown -> MoveDown


module Bindings =

  open App

  let moveUpMsg (m, s) =
    match m.Counters |> List.tryHead with
    | Some c when c.Id <> s.Id ->
        OutMoveUp |> OutMsg |> Some
    | _ -> None

  let moveDownMsg (m, s) =
    match m.Counters |> List.tryLast with
    | Some c when c.Id <> s.Id ->
        OutMoveDown |> OutMsg |> Some
    | _ -> None

  let counterBindings () : Binding<Model * Identifiable<Counter>, InOutMsg<CounterMsg, OutMsg>> list = [
    "CounterIdText" |> Binding.oneWay(fun (_, s) -> s.Id)

    "CounterValue" |> Binding.oneWay(fun (_, s) -> s.Value.Count)
    "Increment" |> Binding.cmd(Increment |> InMsg)
    "Decrement" |> Binding.cmd(Decrement |> InMsg)
    "StepSize" |> Binding.twoWay(
      (fun (_, s) -> float s.Value.StepSize),
      (fun v _ -> v |> int |> SetStepSize |> InMsg))
    "Reset" |> Binding.cmdIf(
      Reset |> InMsg,
      (fun (_, s) -> Counter.canReset s.Value))

    "Remove" |> Binding.cmd(OutRemove |> OutMsg)

    "MoveUp" |> Binding.cmdIf moveUpMsg
    "MoveDown" |> Binding.cmdIf moveDownMsg

    "GlobalState" |> Binding.oneWay(fun (m, _) -> m.SomeGlobalState)
  ]

  let rootBindings () : Binding<Model, Msg> list = [
    "Counters" |> Binding.subModelSeq(
      (fun m -> m.Counters),
      (fun ic -> ic.Id),
      (fun (cId, inOutMsg) ->
        match inOutMsg with
        | InMsg msg -> (cId, msg) |> CounterMsg
        | OutMsg msg -> cId |> mapOutMsg msg),
      counterBindings)

    "ToggleGlobalState" |> Binding.cmd ToggleGlobalState

    "AddCounter" |> Binding.cmd AddCounter
  ]


let mainDesignVm = ViewModel.designInstance (App.init ()) (Bindings.rootBindings ())

let main window =
  Program.mkSimpleWpf App.init App.update Bindings.rootBindings
  |> Program.withConsoleTrace
  |> Program.runWindowWithConfig
    { ElmConfig.Default with LogConsole = true; Measure = true }
    window
