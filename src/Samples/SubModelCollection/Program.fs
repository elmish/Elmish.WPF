module Elmish.WPF.Samples.SubModelCollection.Program

open System
open Elmish
open Elmish.WPF


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


module RecCounter =

  type Model =
    { CounterId: Guid
      CounterValue: int
      StepSize: int
      ChildCounters: Model list }

  let init () =
    { CounterId = Guid.NewGuid()
      CounterValue = 0
      StepSize = 1
      ChildCounters = [] }

  type Msg =
    | Increment
    | Decrement
    | SetStepSize of int
    | Reset
    | AddChild
    | Remove of Guid
    | MoveUp of Guid
    | MoveDown of Guid
    | ChildMsg of Guid * Msg

  let rec update msg m =
    match msg with
    | Increment -> { m with CounterValue = m.CounterValue + m.StepSize }
    | Decrement -> { m with CounterValue = m.CounterValue - m.StepSize }
    | SetStepSize x -> { m with StepSize = x }
    | Reset -> init ()
    | AddChild -> { m with ChildCounters = m.ChildCounters @ [ init () ] }
    | Remove cid ->
        { m with ChildCounters = m.ChildCounters |> List.filter (fun c -> c.CounterId <> cid) }
    | MoveUp cid ->
        let idx = m.ChildCounters |> List.findIndex (fun c -> c.CounterId = cid )
        { m with ChildCounters = m.ChildCounters |> moveUp idx }
    | MoveDown cid ->
        let idx = m.ChildCounters |> List.findIndex (fun c -> c.CounterId = cid )
        { m with ChildCounters = m.ChildCounters |> moveDown idx }
    | ChildMsg (cId, cMsg) ->
        { m with ChildCounters = m.ChildCounters |> List.map (updateSpecific cId cMsg) }

  and updateSpecific id msg m =
    if m.CounterId = id then update msg m else m

  let rec bindings () =
    [
      "CounterId" |> Binding.oneWay (fun m -> m.CounterId)
      "CounterValue" |> Binding.oneWay (fun m -> m.CounterValue)
      "Increment" |> Binding.cmd (fun m -> Increment)
      "Decrement" |> Binding.cmd (fun m -> Decrement)
      "StepSize" |> Binding.twoWay (fun m -> float m.StepSize) (fun v m -> SetStepSize <| int v)
      "Reset" |> Binding.cmd (fun m -> Reset)
      "Remove" |> Binding.paramCmd (fun p m -> p :?> Guid |> Remove)
      "AddChild" |> Binding.cmd (fun m -> AddChild)
      "MoveUp" |> Binding.paramCmdIf
        (fun p m -> p :?> Guid |> MoveUp)
        (fun p m ->
          match m.ChildCounters with
          | c :: _ when c.CounterId <> (p :?> Guid) -> true
          | _ -> false
        )
        false
      "MoveDown" |> Binding.paramCmdIf
        (fun p m -> p :?> Guid |> MoveDown)
        (fun p m ->
          match m.ChildCounters |> List.rev with
          | c :: _ when c.CounterId <> (p :?> Guid) -> true
          | _ -> false
        )
        false
      "ChildCounters" |> Binding.subModelSeq
        (fun m -> m.ChildCounters)
        (fun m -> m.CounterId)
        bindings
        ChildMsg
    ]


module App =

  type Model =
    { Counters: RecCounter.Model list }

  let init () =
    { Counters = [ RecCounter.init () ] }

  type Msg =
    | AddCounter
    | Remove of Guid
    | MoveUp of Guid
    | MoveDown of Guid
    | CounterMsg of Guid * RecCounter.Msg

  let update msg m =
    match msg with
    | AddCounter -> { m with Counters = m.Counters @ [ RecCounter.init () ] }
    | Remove cid ->
        { m with Counters = m.Counters |> List.filter (fun c -> c.CounterId <> cid) }
    | MoveUp cid ->
        let idx = m.Counters |> List.findIndex (fun c -> c.CounterId = cid )
        { m with Counters = m.Counters |> moveUp idx }
    | MoveDown cid ->
        let idx = m.Counters |> List.findIndex (fun c -> c.CounterId = cid )
        { m with Counters = m.Counters |> moveDown idx }
    | CounterMsg (cId, cMsg) ->
        { m with Counters = m.Counters |> List.map (RecCounter.updateSpecific cId cMsg) }

  let bindings model dispatch =
    [
      "Counters" |> Binding.subModelSeq
        (fun m -> m.Counters)
        (fun cm -> cm.CounterId)
        (fun () -> RecCounter.bindings ())
        CounterMsg
      "AddCounter" |> Binding.cmd (fun m -> AddCounter)
      /// TODO: Remove, MoveUp, and MoveDown don't work because I'm not sure how
      /// to set up the bindings from XAML. https://stackoverflow.com/q/51843492/2978652
      "Remove" |> Binding.paramCmd (fun p m -> p :?> Guid |> Remove)
      "MoveUp" |> Binding.paramCmdIf
        (fun p m -> p :?> Guid |> MoveUp)
        (fun p m ->
          match m.Counters with
          | c :: _ when c.CounterId <> (p :?> Guid) -> true
          | _ -> false
        )
        false
      "MoveDown" |> Binding.paramCmdIf
        (fun p m -> p :?> Guid |> MoveDown)
        (fun p m ->
          match m.Counters |> List.rev with
          | c :: _ when c.CounterId <> (p :?> Guid) -> true
          | _ -> false
        )
        false
    ]


[<EntryPoint; STAThread>]
let main argv =
  Program.mkSimple App.init App.update App.bindings
  |> Program.withConsoleTrace
  |> Program.runWindowWithConfig
      { ElmConfig.Default with LogConsole = true }
      (MainWindow())
