module Elmish.WPF.Samples.SubModelSeq.Program

open System
open Elmish
open Elmish.WPF


module Option =

  let set a = Option.map (fun _ -> a)


module Func =

  let flip f b a = f a b

  let applyIf p f a =
    if p a then f a else a


module FuncOption =

  let inputIfNone f a = a |> f |> Option.defaultValue a

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
    let applyIf p f = f |> map |> Func.applyIf p
    let applyIfId id f = f |> applyIf (fun m -> m.Id = id)


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



[<AutoOpen>]
module RoseTree =

  type RoseTree<'a> =
    { Data: 'a
      Children: RoseTree<'a> list }


  module RoseTree =

    let getData m = m.Data
    let rec mapData f t =
      { Data = t.Data |> f
        Children = t.Children |> (f |> mapData |> List.map) }

    let getChildren m = m.Children
    let setChildren v m = { m with Children = v }
    let mapChildren f = map getChildren setChildren f

    let rec map f t =
      t |> f |> mapChildren (f |> map |> List.map)

    let asLeaf a =
      { Data = a
        Children = [] }

    let addChild a = a |> asLeaf |> List.cons |> mapChildren


module App =

  type Model =
    { SomeGlobalState: bool
      DummyRoot: RoseTree<Identifiable<Counter>> }

  type Msg =
    | ToggleGlobalState
    | AddChild of parent: Guid
    | CounterMsg of Guid * CounterMsg
    | Remove of Guid
    | MoveUp of Guid
    | MoveDown of Guid


  let getSomeGlobalState m = m.SomeGlobalState
  let setSomeGlobalState v m = { m with SomeGlobalState = v }
  let mapSomeGlobalState f = f |> map getSomeGlobalState setSomeGlobalState

  let getDummyRoot m = m.DummyRoot
  let setDummyRoot v m = { m with DummyRoot = v }
  let mapDummyRoot f = f |> map getDummyRoot setDummyRoot

  let createIdentifiableCounter () =
    { Id = Guid.NewGuid ()
      Value = Counter.init }

  let init () =
    { SomeGlobalState = false
      DummyRoot =
        createIdentifiableCounter () // Placeholder data to satisfy type system. User never sees this.
        |> RoseTree.asLeaf
        |> RoseTree.addChild (createIdentifiableCounter ()) }

  let hasId id n = n.Data.Id = id

  let addChildCounter nId =
    { Id = nId
      Value = Counter.init }
    |> RoseTree.addChild

  let swapCounters swap nId =
    nId
    |> hasId
    |> List.tryFindIndex
    |> FuncOption.bind swap
    |> FuncOption.inputIfNone
    |> RoseTree.mapChildren

  let update = function
    | ToggleGlobalState -> mapSomeGlobalState not
    | CounterMsg (nId, msg) -> msg |> Counter.update |> Identifiable.applyIfId nId |> RoseTree.mapData |> mapDummyRoot
    | AddChild pId -> Guid.NewGuid () |> addChildCounter |> Func.applyIf (hasId pId) |> RoseTree.map |> mapDummyRoot
    | Remove nId -> nId |> hasId >> not |> List.filter |> RoseTree.mapChildren |> RoseTree.map |> mapDummyRoot
    | MoveUp nId -> nId |> swapCounters List.swapWithPrev |> RoseTree.map |> mapDummyRoot
    | MoveDown nId -> nId |> swapCounters List.swapWithNext |> RoseTree.map |> mapDummyRoot


module Bindings =

  open App

  let canMoveUp (p, c) =
    p.Children
    |> List.tryHead
    |> Option.map (fun c -> c.Data.Id)
    |> Option.filter ((<>) c.Data.Id)
    |> Option.set (MoveUp c.Data.Id)

  let canMoveDown (p, c) =
    p.Children
    |> List.tryLast
    |> Option.map (fun c -> c.Data.Id)
    |> Option.filter ((<>) c.Data.Id)
    |> Option.set (MoveDown c.Data.Id)

  let rec counterTreeBindings () : Binding<Model * (RoseTree<Identifiable<Counter>> * RoseTree<Identifiable<Counter>>), Msg> list = [
    "CounterIdText" |> Binding.oneWay(fun (_, (_, c)) -> c.Data.Id)
  
    "CounterValue" |> Binding.oneWay(fun (_, (_, c)) -> c.Data.Value.Count)
    "Increment" |> Binding.cmd(fun (_, (_, c)) -> CounterMsg (c.Data.Id, Increment))
    "Decrement" |> Binding.cmd(fun (_, (_, c)) -> CounterMsg (c.Data.Id, Decrement))
    "StepSize" |> Binding.twoWay(
      (fun (_, (_, c)) -> float c.Data.Value.StepSize),
      (fun v (_, (_, c)) -> CounterMsg (c.Data.Id, SetStepSize (int v))))
    "Reset" |> Binding.cmdIf(
      (fun (_, (_, c)) -> CounterMsg (c.Data.Id, Reset)),
      (fun (_, (_, c)) -> Counter.canReset c.Data.Value))
  
    "Remove" |> Binding.cmd(fun (_, (_, c)) -> Remove c.Data.Id)
    "AddChild" |> Binding.cmd(fun (_, (_, c)) -> AddChild c.Data.Id)
  
    "MoveUp" |> Binding.cmdIf(snd >> canMoveUp)
    "MoveDown" |> Binding.cmdIf(snd >> canMoveDown)
  
    "GlobalState" |> Binding.oneWay(fun (m, _) -> m.SomeGlobalState)
  
    "ChildCounters" |> Binding.subModelSeq(
      (fun (_, (_, c)) -> c.Children |> Seq.map (fun gc -> (c, gc))),
      (fun ((m, _), childCounter) -> (m, childCounter)),
      (fun (_, (_, c)) -> c.Data.Id),
      snd,
      counterTreeBindings)
  ]


  let rootBindings () : Binding<Model, Msg> list = [
    "Counters" |> Binding.subModelSeq(
      (fun m -> m.DummyRoot.Children |> Seq.map (fun c -> (m.DummyRoot, c))),
      (fun (_, c) -> c.Data.Id),
      counterTreeBindings)
  
    "ToggleGlobalState" |> Binding.cmd ToggleGlobalState
  
    "AddCounter" |> Binding.cmd(fun m -> AddChild m.DummyRoot.Data.Id)
  ]

let mainDesignVm = ViewModel.designInstance (App.init ()) (Bindings.rootBindings ())

let main window =
  Program.mkSimpleWpf App.init App.update Bindings.rootBindings
  |> Program.withConsoleTrace
  |> Program.runWindowWithConfig
    { ElmConfig.Default with LogConsole = true; Measure = true }
    window
