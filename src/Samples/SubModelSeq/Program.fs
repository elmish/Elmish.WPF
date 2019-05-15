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



module App =

  type Model =
    { SomeGlobalState: bool
      AllCounters: Counter list
      ParentChild: (CounterId * CounterId) list }

  let init () =
    { SomeGlobalState = false
      AllCounters = [ Counter.create () ]
      ParentChild = [] }

  /// Indicates whether the counter is a child counter.
  let isChild m c =
    m.ParentChild
    |> List.exists (fun (pid, cid) -> cid = c.Id)

  /// Returns all top-level counters.
  let topLevelCounters m =
    m.AllCounters
    |> List.filter (not << isChild m)

  /// Returns all immediate child counters of the specified parent counter ID.
  let childrenOf parentId m =
    m.AllCounters
    |> List.filter (fun child -> m.ParentChild |> List.contains (parentId, child.Id))

  /// Returns all recursive child counters of the specified parent counter ID.
  let rec recursiveChildrenOf parentId m =
    let immediateChildren = childrenOf parentId m
    immediateChildren @ (immediateChildren |> List.collect (fun c -> recursiveChildrenOf c.Id m))

  /// Returns the parent counter ID of the specified child counter ID.
  let parentIdOf childId m =
    m.ParentChild
    |> List.tryPick (function (pid, cid) when cid = childId -> Some pid | _ -> None)

  /// Returns the sibling counters of the specified counter ID.
  let getSiblings counterId m =
    match parentIdOf counterId m with
    | None -> topLevelCounters m
    | Some pid -> childrenOf pid m

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

    | AddCounter None -> { m with AllCounters = m.AllCounters @ [ Counter.create () ] }

    | AddCounter (Some parentId) ->
        let child = Counter.create ()
        { m with
            AllCounters = m.AllCounters @ [child]
            ParentChild = m.ParentChild @ [ (parentId, child.Id) ] }

    | Increment cid -> { m with AllCounters = m.AllCounters |> mapCounter cid Counter.increment }

    | Decrement cid -> { m with AllCounters = m.AllCounters |> mapCounter cid Counter.decrement }

    | SetStepSize (cid, step) -> { m with AllCounters = m.AllCounters |> mapCounter cid (Counter.setStepSize step) }

    | Reset cid -> { m with AllCounters = m.AllCounters |> mapCounter cid Counter.reset }

    | Remove cid ->
        let childIds = recursiveChildrenOf cid m |> List.map (fun c -> c.Id)
        let idsToRemove = cid :: childIds |> Set.ofList
        { m with
            AllCounters = m.AllCounters |> List.filter (fun c -> not <| idsToRemove.Contains c.Id)
            ParentChild = m.ParentChild |> List.filter (fun (pid, cid) ->
              not <| idsToRemove.Contains pid && not <| idsToRemove.Contains cid)
        }

    // TODO: moving up/down must be done at the correct level of hierarchy;
    // currently if the hierarchy is A (B, C), D (where B and C are children of A)
    // then D must be moved up three times before it's placed before A
    | MoveUp cid -> { m with AllCounters = m.AllCounters |> moveCounter cid moveUp }
    | MoveDown cid -> { m with AllCounters = m.AllCounters |> moveCounter cid moveDown }


module Bindings =

  open App

  let rec counterBindings () : Binding<Model * Counter, Msg> list = [
    "CounterIdText" |> Binding.oneWay (fun (m, { Id = CounterId cid}) -> cid)

    "CounterId" |> Binding.oneWay (fun (m, c) -> c.Id)

    "CounterValue" |> Binding.oneWay (fun (m, c) -> c.CounterValue)

    "Increment" |> Binding.cmd (fun (m, c) -> Increment c.Id)

    "Decrement" |> Binding.cmd (fun (m, c) -> Decrement c.Id)

    "StepSize" |> Binding.twoWay
      (fun (m, c) -> float c.StepSize)
      (fun v (m, c) -> SetStepSize (c.Id, int v))

    "Reset" |> Binding.cmd (fun (m, c) -> Reset c.Id)

    "Remove" |> Binding.cmd (fun (m, c) -> Remove c.Id)

    "AddChild" |> Binding.cmd (fun (m, c) -> AddCounter (Some c.Id))

    "MoveUp" |> Binding.cmdIf
      (fun (m, c) -> MoveUp c.Id)
      (fun (m, c) -> m |> getSiblings c.Id |> List.tryHead <> Some c)

    "MoveDown" |> Binding.cmdIf
      (fun (m, c) -> MoveDown c.Id)
      (fun (m, c) -> m |> getSiblings c.Id |> List.tryLast <> Some c)

    "GlobalState" |> Binding.oneWay (fun (m, c) -> m.SomeGlobalState)

    "ChildCounters" |> Binding.subModelSeq
      (fun (m, c) -> childrenOf c.Id m)
      (fun ((m, parentCounter), childCounter) -> (m, childCounter))
      (fun (m, c) -> c.Id)
      snd
      counterBindings
  ]


  let rootBindings () : Binding<Model, Msg> list = [
    "Counters" |> Binding.subModelSeq
      (fun m -> topLevelCounters m)
      id
      (fun (m, c) -> c.Id)
      snd
      counterBindings

    "ToggleGlobalState" |> Binding.cmd (fun m -> ToggleGlobalState)

    "AddCounter" |> Binding.cmd (fun m -> AddCounter None)
  ]


[<EntryPoint; STAThread>]
let main argv =
  Program.mkSimple App.init App.update (fun _ _ -> Bindings.rootBindings ())
  |> Program.withConsoleTrace
  |> Program.runWindowWithConfig
      { ElmConfig.Default with LogConsole = true; Measure = true }
      (MainWindow())
