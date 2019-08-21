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

    let reset c =
      { create () with Id = c.Id }


module Tree =

  type Node<'a> =
    { Data: 'a
      Children: Node<'a> list }

  let asLeaf a =
    { Data = a
      Children = [] }

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

  let allPreorderFlatten m =
    m.DummyRoot |> Tree.preorderFlatten |> List.skip 1

  let allCounters m =
    m |> allPreorderFlatten |> List.map (fun n -> n.Data)

  let parentChild m =
    m |> allPreorderFlatten |> List.collect (fun pn -> pn.Children |> List.map (fun cn -> (pn.Data.Id, cn.Data.Id)))

  /// Returns all top-level counters.
  let topLevelCounters m =
    m.DummyRoot.Children |> List.map (fun c -> c.Data)

  /// Returns all immediate child counters of the specified parent counter ID.
  let childrenOf parentId m =
    match parentId with
    | Some pid -> m |> allCounters |> List.filter (fun child -> m |> parentChild |> List.contains (pid, child.Id))
    | None     -> topLevelCounters m

  /// Returns the parent counter ID of the specified child counter ID.
  let parentIdOf childId m =
    m |> parentChild
    |> List.tryPick (function (pid, cid) when cid = childId -> Some pid | _ -> None)

  /// Returns the sibling counters of the specified counter ID.
  let childrenOfParentOf cid m =
    let pid = m |> parentIdOf cid
    m |> childrenOf pid

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
  let updateCounter cid f (c: Counter) =
    if c.Id = cid then f c else c

  /// Updates the counter with the specified ID using the specified function.
  let mapCounter cid f counters =
    counters |> List.map (updateCounter cid f)

  /// In the specified list, moves the counter with the specified ID using the specified
  /// function.
  let moveCounter cid moveFun counters =
    let idx = counters |> List.findIndex (fun c -> c.Id = cid)
    counters |> moveFun idx

  let update msg m =
    match msg with
    | ToggleGlobalState -> { m with SomeGlobalState = not m.SomeGlobalState }

    | AddCounter parentId ->
        let f (n:Tree.Node<Counter>) =
          if n.Data.Id = parentId
          then { n with Children = (Counter.create () |> Tree.asLeaf) :: n.Children}
          else n
        { m with DummyRoot = m.DummyRoot |> Tree.map f }

    | Increment cid -> { m with DummyRoot = m.DummyRoot |> Tree.mapData (updateCounter cid Counter.increment) }

    | Decrement cid -> { m with DummyRoot = m.DummyRoot |> Tree.mapData (updateCounter cid Counter.decrement) }

    | SetStepSize (cid, step) -> { m with DummyRoot = m.DummyRoot |> Tree.mapData (updateCounter cid (Counter.setStepSize step)) }

    | Reset cid -> { m with DummyRoot = m.DummyRoot |> Tree.mapData (updateCounter cid Counter.reset) }

    | Remove cid ->
        let f (n:Tree.Node<Counter>) =
          { n with Children = n.Children |> List.filter (fun n -> n.Data.Id <> cid) }
        { m with DummyRoot = m.DummyRoot |> Tree.map f }

    | MoveUp id ->
      let f (n:Tree.Node<Counter>) =
        let oi = n.Children |> List.tryFindIndex (fun nn -> nn.Data.Id = id)
        match oi with
        | Some i -> { n with Children = n.Children |> List.swap i (i - 1) }
        | None -> n
      { m with DummyRoot = m.DummyRoot |> Tree.map f }

    | MoveDown id ->
      let f (n:Tree.Node<Counter>) =
        let oi = n.Children |> List.tryFindIndex (fun nn -> nn.Data.Id = id)
        match oi with
        | Some i -> { n with Children = n.Children |> List.swap i (i + 1) }
        | None -> n
      { m with DummyRoot = m.DummyRoot |> Tree.map f }


module Bindings =

  open App

  let rec counterBindings () : Binding<Model * Counter, Msg> list = [
    "CounterIdText" |> Binding.oneWay(fun (m, { Id = CounterId cid}) -> cid)

    "CounterId" |> Binding.oneWay(fun (m, c) -> c.Id)

    "CounterValue" |> Binding.oneWay(fun (m, c) -> c.CounterValue)

    "Increment" |> Binding.cmd(fun (m, c) -> Increment c.Id)

    "Decrement" |> Binding.cmd(fun (m, c) -> Decrement c.Id)

    "StepSize" |> Binding.twoWay(
      (fun (m, c) -> float c.StepSize),
      (fun v (m, c) -> SetStepSize (c.Id, int v)))

    "Reset" |> Binding.cmd(fun (m, c) -> Reset c.Id)

    "Remove" |> Binding.cmd(fun (m, c) -> Remove c.Id)

    "AddChild" |> Binding.cmd(fun (m, c) -> AddCounter c.Id)

    "MoveUp" |> Binding.cmdIf(
      (fun (_, c) -> MoveUp c.Id),
      (fun (m, c) -> m |> childrenOfParentOf c.Id |> List.tryHead <> Some c))

    "MoveDown" |> Binding.cmdIf(
      (fun (m, c) -> MoveDown c.Id),
      (fun (m, c) -> m |> childrenOfParentOf c.Id |> List.tryLast <> Some c))

    "GlobalState" |> Binding.oneWay(fun (m, c) -> m.SomeGlobalState)

    "ChildCounters" |> Binding.subModelSeq(
      (fun (m, c) -> childrenOf (Some c.Id) m),
      (fun ((m, _), childCounter) -> (m, childCounter)),
      (fun (_, c) -> c.Id),
      snd,
      counterBindings)
  ]


  let rootBindings () : Binding<Model, Msg> list = [
    "Counters" |> Binding.subModelSeq(
      (fun m -> topLevelCounters m),
      (fun c -> c.Id),
      counterBindings)

    "ToggleGlobalState" |> Binding.cmd ToggleGlobalState

    "AddCounter" |> Binding.cmd(fun m -> AddCounter m.DummyRoot.Data.Id)
  ]


[<EntryPoint; STAThread>]
let main argv =
  Program.mkSimpleWpf App.init App.update Bindings.rootBindings
  |> Program.withConsoleTrace
  |> Program.runWindowWithConfig
      { ElmConfig.Default with LogConsole = true; Measure = true }
      (MainWindow())
