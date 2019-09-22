module Elmish.WPF.Samples.SubModelSeq.Program

open System
open Elmish
open Elmish.WPF

module List =

  let swap i j =
    List.permute
      (function
        | a when a = i -> j
        | a when a = j -> i
        | a -> a)


[<AutoOpen>]
module Domain =

  type CounterId = CounterId of Guid

  type Counter =
    { Id: CounterId
      CounterValue: int
      StepSize: int }

  module Counter =

    let create () =
      { Id = Guid.NewGuid () |> CounterId
        CounterValue = 0
        StepSize = 1 }

    let increment c =
      { c with CounterValue = c.CounterValue + c.StepSize }

    let decrement c =
      { c with CounterValue = c.CounterValue - c.StepSize}

    let setStepSize step c =
      { c with StepSize = step }

    let canReset c =
      c.CounterValue <> 0 || c.StepSize <> 1

    let reset c =
      { create () with Id = c.Id }


module Tree =

  type Node<'a> =
    { Data: 'a
      Children: Node<'a> list }

  let asLeaf a =
    { Data = a
      Children = [] }

  let dataOfChildren n =
    n.Children |> List.map (fun nn -> nn.Data)

  let rec map f n =
    n |> f |> (fun nn -> { nn with Children = nn.Children |> List.map (map f) } )

  let rec mapData f n =
    { Data = n.Data |> f
      Children = n.Children |> List.map (mapData f) }

  let rec preorderFlatten n =
    n :: List.collect preorderFlatten n.Children

module App =

  type Model =
    { SomeGlobalState: bool
      DummyRoot: Tree.Node<Counter> }

  let asDummyRoot ns : Tree.Node<Counter> =
    { Data = Counter.create() // Placeholder data to satisfy type system. User never sees this.
      Children = ns }

  let init () =
    { SomeGlobalState = false
      DummyRoot = [ Counter.create() |> Tree.asLeaf ] |> asDummyRoot }

  /// Returns all top-level counters.
  let topLevelCounters m =
    m.DummyRoot |> Tree.dataOfChildren

  /// Returns all immediate child counters of the specified parent counter ID.
  let childrenCountersOf pid m =
    m.DummyRoot
    |> Tree.preorderFlatten
    |> List.find (fun n -> n.Data.Id = pid)
    |> Tree.dataOfChildren

  /// Returns the parent of the specified child counter ID.
  let parentOf cid m =
    m.DummyRoot
    |> Tree.preorderFlatten
    |> List.find (fun n -> n |> Tree.dataOfChildren |> List.map (fun d -> d.Id) |> List.contains cid)

  /// Returns the sibling counters of the specified counter ID.
  let childrenCountersOfParentOf cid =
    parentOf cid >> Tree.dataOfChildren

  type Msg =
    | ToggleGlobalState
    | AddCounter of parent: CounterId
    | Increment of CounterId
    | Decrement of CounterId
    | SetStepSize of CounterId * int
    | Reset of CounterId
    | Remove of CounterId
    | MoveUp of CounterId
    | MoveDown of CounterId

  /// Updates the counter using the specified function if the ID matches,
  /// otherwise passes the counter through unmodified.
  let updateCounter f id c =
    if c.Id = id then f c else c

  let incrementCounter = updateCounter Counter.increment
  let decrementCounter = updateCounter Counter.decrement
  let setStepSizeOfCounter ss = updateCounter <| Counter.setStepSize ss
  let resetCounter = updateCounter Counter.reset

  let update msg m =
    match msg with
    | ToggleGlobalState -> { m with SomeGlobalState = not m.SomeGlobalState }

    | AddCounter pid ->
        let f (n: Tree.Node<Counter>) =
          if n.Data.Id = pid
          then { n with Children = (Counter.create () |> Tree.asLeaf) :: n.Children}
          else n
        { m with DummyRoot = m.DummyRoot |> Tree.map f }

    | Increment id -> { m with DummyRoot = m.DummyRoot |> Tree.mapData (incrementCounter id) }

    | Decrement id -> { m with DummyRoot = m.DummyRoot |> Tree.mapData (decrementCounter id) }

    | SetStepSize (id, ss) -> { m with DummyRoot = m.DummyRoot |> Tree.mapData (setStepSizeOfCounter ss id) }

    | Reset id -> { m with DummyRoot = m.DummyRoot |> Tree.mapData (resetCounter id ) }

    | Remove id ->
        let f (n: Tree.Node<Counter>) =
          { n with Children = n.Children |> List.filter (fun n -> n.Data.Id <> id) }
        { m with DummyRoot = m.DummyRoot |> Tree.map f }

    | MoveUp id ->
      let f (n: Tree.Node<Counter>) =
        match n.Children |> List.tryFindIndex (fun nn -> nn.Data.Id = id) with
        | Some i -> { n with Children = n.Children |> List.swap i (i - 1) }
        | None -> n
      { m with DummyRoot = m.DummyRoot |> Tree.map f }

    | MoveDown id ->
      let f (n: Tree.Node<Counter>) =
        match n.Children |> List.tryFindIndex (fun nn -> nn.Data.Id = id) with
        | Some i -> { n with Children = n.Children |> List.swap i (i + 1) }
        | None -> n
      { m with DummyRoot = m.DummyRoot |> Tree.map f }


module Bindings =

  open App

  let rec counterBindings () : Binding<Model * Counter, Msg> list = [
    "CounterIdText" |> Binding.oneWay(fun (_, { Id = CounterId id }) -> id)

    "CounterValue" |> Binding.oneWay(fun (_, c) -> c.CounterValue)

    "Increment" |> Binding.cmd(fun (_, c) -> Increment c.Id)

    "Decrement" |> Binding.cmd(fun (_, c) -> Decrement c.Id)

    "StepSize" |> Binding.twoWay(
      (fun (_, c) -> float c.StepSize),
      (fun v (_, c) -> SetStepSize (c.Id, int v)))

    "Reset" |> Binding.cmdIf((fun (_, c) -> Reset c.Id), (fun (_, c) -> Counter.canReset c))

    "Remove" |> Binding.cmd(fun (_, c) -> Remove c.Id)

    "AddChild" |> Binding.cmd(fun (_, c) -> AddCounter c.Id)

    "MoveUp" |> Binding.cmdIf(
      (fun (_, c) -> MoveUp c.Id),
      (fun (m, c) -> m |> childrenCountersOfParentOf c.Id |> List.tryHead <> Some c))

    "MoveDown" |> Binding.cmdIf(
      (fun (_, c) -> MoveDown c.Id),
      (fun (m, c) -> m |> childrenCountersOfParentOf c.Id |> List.tryLast <> Some c))

    "GlobalState" |> Binding.oneWay(fun (m, _) -> m.SomeGlobalState)

    "ChildCounters" |> Binding.subModelSeq(
      (fun (m, c) -> m |> childrenCountersOf c.Id),
      (fun ((m, _), childCounter) -> (m, childCounter)),
      (fun (_, c) -> c.Id),
      snd,
      counterBindings)
  ]


  let rootBindings () : Binding<Model, Msg> list = [
    "Counters" |> Binding.subModelSeq(
      (fun m -> m |> topLevelCounters),
      (fun c -> c.Id),
      counterBindings)

    "ToggleGlobalState" |> Binding.cmd ToggleGlobalState

    "AddCounter" |> Binding.cmd(fun m -> AddCounter m.DummyRoot.Data.Id)
  ]


[<EntryPoint; STAThread>]
let main _ =
  Program.mkSimpleWpf App.init App.update Bindings.rootBindings
  |> Program.withConsoleTrace
  |> Program.runWindowWithConfig
    { ElmConfig.Default with LogConsole = true; Measure = true }
    (MainWindow())
