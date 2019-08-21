module Elmish.WPF.Samples.SubModelSeq.Program

open System
open Elmish
open Elmish.WPF


[<AutoOpen>]
module Utils =

  /// Returns a list where the item with the specified index is moved up one index
  /// unless it is already the first item in the list. Throws if the index does not
  /// exist in the list.
  let moveUp idx list =
    if idx = 0 then list
    else
      let array = List.toArray list
      let el = array.[idx]
      array.[idx] <- array.[idx-1]
      array.[idx-1] <- el
      Array.toList array

  /// Returns a list where the item with the specified index is moved up one index
  /// unless it is already the last item in the list. Throws if the index does not
  /// exist in the list.
  let moveDown idx list =
    if idx = List.length list - 1 then list
    else
      let array = List.toArray list
      let el = array.[idx]
      array.[idx] <- array.[idx+1]
      array.[idx+1] <- el
      Array.toList array


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

  let rec mapData f n =
    { Data = n.Data |> f
      Children = n.Children |> List.map (mapData f) }

  let rec mapChildren f n =
    { Data = n.Data
      Children = n.Children |> f |> List.map (mapChildren f)}

  let rec preorderFlatten n =
    n :: List.collect preorderFlatten n.Children

module App =

  type Model =
    { SomeGlobalState: bool
      Counters: Tree.Node<Counter> list }

  let init () =
    { SomeGlobalState = false
      Counters = [ Counter.create() |> Tree.asLeaf ] }

  let allPreorderFlatten m =
    m.Counters |> List.collect Tree.preorderFlatten

  let allCounters m =
    m |> allPreorderFlatten |> List.map (fun n -> n.Data)

  let parentChild m =
    m |> allPreorderFlatten |> List.collect (fun pn -> pn.Children |> List.map (fun cn -> (pn.Data.Id, cn.Data.Id)))

  /// Returns all top-level counters.
  let topLevelCounters m =
    m.Counters |> List.map (fun c -> c.Data)

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
    | AddCounter of parent: CounterId option
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

    | AddCounter None -> { m with Counters = (Counter.create () |> Tree.asLeaf) :: m.Counters }

    | AddCounter (Some parentId) ->
        let child = Counter.create () |> Tree.asLeaf
        let rec f =
          fun (n:Tree.Node<Counter>) ->
            if n.Data.Id = parentId
            then { n with Children = child :: n.Children }
            else { n with Children = n.Children |> List.map f }
        { m with Counters = m.Counters |> List.map f }

    | Increment cid -> { m with Counters = m.Counters |> List.map (Tree.mapData (updateCounter cid Counter.increment)) }

    | Decrement cid -> { m with Counters = m.Counters |> List.map (Tree.mapData (updateCounter cid Counter.decrement)) }

    | SetStepSize (cid, step) -> { m with Counters = m.Counters |> List.map (Tree.mapData (updateCounter cid (Counter.setStepSize step))) }

    | Reset cid -> { m with Counters = m.Counters |> List.map (Tree.mapData (updateCounter cid Counter.reset)) }

    | Remove cid ->
        let f (ns:Tree.Node<Counter> list) = ns |> List.filter (fun n -> n.Data.Id <> cid)
        { m with Counters = m.Counters |> f |> List.map (Tree.mapChildren f) }

    | MoveUp id ->
      let f (ns:Tree.Node<Counter> list) : Tree.Node<Counter> list =
        let oi = ns |> List.tryFindIndex (fun n -> n.Data.Id = id)
        match oi with
        | Some i -> (ns |> List.take (i - 1)) @ [ns |> List.item i] @ [ns |> List.item (i - 1)] @ (ns |> List.skip (i + 1))
        | None -> ns
      { m with Counters = m.Counters |> f |> List.map (Tree.mapChildren f) }

    | MoveDown id ->
      let f (ns:Tree.Node<Counter> list) =
        let oi = ns |> List.tryFindIndex (fun n -> n.Data.Id = id)
        match oi with
        | Some i -> (ns |> List.take i) @ [ns |> List.item (i + 1)] @ [ns |> List.item i] @ (ns |> List.skip (i + 2))
        | None -> ns
      { m with Counters = m.Counters |> f |> List.map (Tree.mapChildren f) }


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

    "AddChild" |> Binding.cmd(fun (m, c) -> AddCounter (Some c.Id))

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

    "AddCounter" |> Binding.cmd (AddCounter None)
  ]


[<EntryPoint; STAThread>]
let main argv =
  Program.mkSimpleWpf App.init App.update Bindings.rootBindings
  |> Program.withConsoleTrace
  |> Program.runWindowWithConfig
      { ElmConfig.Default with LogConsole = true; Measure = true }
      (MainWindow())
