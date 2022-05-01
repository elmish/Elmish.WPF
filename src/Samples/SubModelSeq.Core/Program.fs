module Elmish.WPF.Samples.SubModelSeq.Program

open System
open Serilog
open Serilog.Extensions.Logging
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

    let bindings () : Binding<Counter, CounterMsg, obj> list = [
      "CounterValue" |> Binding.oneWay (fun m -> m.Count)
      "Increment" |> Binding.cmd Increment
      "Decrement" |> Binding.cmd Decrement
      "StepSize" |> Binding.twoWay(
        (fun m -> float m.StepSize),
        int >> SetStepSize)
      "Reset" |> Binding.cmdIf(Reset, canReset)
    ]


[<AutoOpen>]
module RoseTree =

  type RoseTree<'model> =
    { Data: 'model
      Children: RoseTree<'model> list }

  type RoseTreeMsg<'a, 'msg> =
    | BranchMsg of 'a * RoseTreeMsg<'a, 'msg>
    | LeafMsg of 'msg

  module RoseTree =

    let create data children =
      { Data = data
        Children = children }
    let createLeaf a = create a []

    let getData t = t.Data
    let setData (d: 'a) (t: RoseTree<'a>) = { t with Data = d }
    let mapData f = map getData setData f

    let getChildren t = t.Children
    let setChildren c t = { t with Children = c }
    let mapChildren f = map getChildren setChildren f

    let addSubtree t = t |> List.cons |> mapChildren
    let addChildData a = a |> createLeaf |> addSubtree

    let update p (f: 'msg -> RoseTree<'model> -> RoseTree<'model>) =
      let rec updateRec = function
        | BranchMsg (a, msg) -> msg |> updateRec |> List.mapFirst (p a) |> mapChildren
        | LeafMsg msg -> msg |> f
      updateRec


module App =

  type Model =
    { SomeGlobalState: bool
      DummyRoot: RoseTree<Identifiable<Counter>> }

  type SubtreeMsg =
    | CounterMsg of CounterMsg
    | AddChild
    | Remove of Guid
    | MoveUp of Guid
    | MoveDown of Guid

  type SubtreeOutMsg =
    | OutRemove
    | OutMoveUp
    | OutMoveDown

  type Msg =
    | ToggleGlobalState
    | SubtreeMsg of RoseTreeMsg<Guid, SubtreeMsg>


  let getSomeGlobalState m = m.SomeGlobalState
  let setSomeGlobalState v m = { m with SomeGlobalState = v }
  let mapSomeGlobalState f = f |> map getSomeGlobalState setSomeGlobalState

  let getDummyRoot m = m.DummyRoot
  let setDummyRoot v m = { m with DummyRoot = v }
  let mapDummyRoot f = f |> map getDummyRoot setDummyRoot

  let createNewIdentifiableCounter () =
    { Id = Guid.NewGuid ()
      Value = Counter.init }

  let createNewLeaf () =
    createNewIdentifiableCounter ()
    |> RoseTree.createLeaf

  let init () =
    let dummyRootData = createNewIdentifiableCounter () // Placeholder data to satisfy type system. User never sees this.
    { SomeGlobalState = false
      DummyRoot =
        createNewLeaf ()
        |> List.singleton
        |> RoseTree.create dummyRootData }

  let hasId id t = t.Data.Id = id

  let swapCounters swap nId =
    nId
    |> hasId
    |> List.tryFindIndex
    |> FuncOption.bind swap
    |> FuncOption.inputIfNone

  let updateSubtree = function
    | CounterMsg msg -> msg |> Counter.update |> Identifiable.map |> RoseTree.mapData
    | AddChild -> createNewLeaf () |> List.cons |> RoseTree.mapChildren
    | Remove cId -> cId |> hasId >> not |> List.filter |> RoseTree.mapChildren
    | MoveUp cId -> cId |> swapCounters List.swapWithPrev |> RoseTree.mapChildren
    | MoveDown cId -> cId |> swapCounters List.swapWithNext |> RoseTree.mapChildren

  let update = function
    | ToggleGlobalState -> mapSomeGlobalState not
    | SubtreeMsg msg -> msg |> RoseTree.update hasId updateSubtree |> mapDummyRoot

  let mapOutMsg = function
    | OutRemove -> Remove
    | OutMoveUp -> MoveUp
    | OutMoveDown -> MoveDown


module Bindings =

  open App

  type SelfWithParent<'a> =
    { Self: 'a
      Parent: 'a }

  let moveUpMsg (_, { Parent = p; Self = s }) =
    match p.Children |> List.tryHead with
    | Some c when c.Data.Id <> s.Data.Id ->
        OutMoveUp |> Some
    | _ -> None

  let moveDownMsg (_, { Parent = p; Self = s }) =
    match p.Children |> List.tryLast with
    | Some c when c.Data.Id <> s.Data.Id ->
        OutMoveDown |> Some
    | _ -> None

  let rec subtreeBindings () : Binding<Model * SelfWithParent<RoseTree<Identifiable<Counter>>>, InOutMsg<RoseTreeMsg<Guid, SubtreeMsg>, SubtreeOutMsg>, obj> list =
    let counterBindings =
      Counter.bindings ()
      |> Bindings.mapModel (fun (_, { Self = s }) -> s.Data.Value)
      |> Bindings.mapMsg (CounterMsg >> LeafMsg)

    let inMsgBindings =
      [ "CounterIdText" |> Binding.oneWay(fun (_, { Self = s }) -> s.Data.Id)
        "AddChild" |> Binding.cmd(AddChild |> LeafMsg)
        "GlobalState" |> Binding.oneWay(fun (m, _) -> m.SomeGlobalState)
        "ChildCounters"
          |> Binding.subModelSeq (subtreeBindings, (fun (_, { Self = c }) -> c.Data.Id))
          |> Binding.mapModel (fun (m, { Self = p }) -> p.Children |> Seq.map (fun c -> m, { Self = c; Parent = p }))
          |> Binding.mapMsg (fun (cId, inOutMsg) ->
            match inOutMsg with
            | InMsg msg -> (cId, msg) |> BranchMsg
            | OutMsg msg -> cId |> mapOutMsg msg |> LeafMsg)
      ] @ counterBindings
      |> Bindings.mapMsg InMsg

    let outMsgBindings =
      [ "Remove" |> Binding.cmd OutRemove
        "MoveUp" |> Binding.cmdIf moveUpMsg
        "MoveDown" |> Binding.cmdIf moveDownMsg
      ] |> Bindings.mapMsg OutMsg

    outMsgBindings @ inMsgBindings


  let rootBindings () : Binding<Model, Msg, obj> list = [
    "Counters"
      |> Binding.subModelSeq (subtreeBindings, (fun (_, { Self = c }) -> c.Data.Id))
      |> Binding.mapModel (fun m -> m.DummyRoot.Children |> Seq.map (fun c -> m, { Self = c; Parent = m.DummyRoot }))
      |> Binding.mapMsg (fun (cId, inOutMsg) ->
        match inOutMsg with
        | InMsg msg -> (cId, msg) |> BranchMsg
        | OutMsg msg -> cId |> mapOutMsg msg |> LeafMsg
        |> SubtreeMsg)

    "ToggleGlobalState" |> Binding.cmd ToggleGlobalState

    "AddCounter" |> Binding.cmd (AddChild |> LeafMsg |> SubtreeMsg)
  ]

let counterDesignVm = ViewModel.designInstance Counter.init (Counter.bindings ())
let mainDesignVm = ViewModel.designInstance (App.init ()) (Bindings.rootBindings ())

let main window =
  let logger =
    LoggerConfiguration()
      .MinimumLevel.Override("Elmish.WPF.Update", Events.LogEventLevel.Verbose)
      .MinimumLevel.Override("Elmish.WPF.Bindings", Events.LogEventLevel.Verbose)
      .MinimumLevel.Override("Elmish.WPF.Performance", Events.LogEventLevel.Verbose)
      .WriteTo.Console()
      .CreateLogger()

  WpfProgram.mkSimple App.init App.update Bindings.rootBindings
  |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))
  |> WpfProgram.startElmishLoop window
