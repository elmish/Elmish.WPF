module Elmish.WPF.Samples.SubModelSeq.Program

open System
open Serilog
open Serilog.Extensions.Logging
open Elmish.WPF


module Func =

  let flip f b a = f a b

  let applyIf p f a =
    if p a then f a else a


module FuncOption =

  let inputIfNone f a = a |> f |> Option.defaultValue a

  let bindFunc (f: 'b -> 'a -> 'c) (mb: 'a -> 'b option) a =
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

    let bindings () : Binding<Counter, CounterMsg> list = [
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

  type RoseTree<'a> =
    { Data: 'a
      Children: RoseTree<'a> list }


  module RoseTree =

    let getData m = m.Data
    let setData v m = { Data = v; Children = m.Children }
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
    
    let dataOfChildren t =
      t.Children |> List.map getData
    
    let rec flatten t =
      t :: List.collect flatten t.Children


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
    |> FuncOption.bindFunc swap
    |> FuncOption.inputIfNone
    |> RoseTree.mapChildren

  let update = function
    | ToggleGlobalState -> mapSomeGlobalState not
    | CounterMsg (nId, msg) -> msg |> Counter.update |> Identifiable.applyIfId nId |> RoseTree.mapData |> mapDummyRoot
    | AddChild pId -> Guid.NewGuid () |> addChildCounter |> Func.applyIf (hasId pId) |> RoseTree.map |> mapDummyRoot
    | Remove nId -> nId |> hasId >> not |> List.filter |> RoseTree.mapChildren |> RoseTree.map |> mapDummyRoot
    | MoveUp nId -> nId |> swapCounters List.swapWithPrev |> RoseTree.map |> mapDummyRoot
    | MoveDown nId -> nId |> swapCounters List.swapWithNext |> RoseTree.map |> mapDummyRoot

  /// Returns all top-level counters.
  let topLevelCounters m =
    m.DummyRoot |> RoseTree.dataOfChildren

  /// Returns all immediate child counters of the specified parent counter ID.
  let childCountersOf pid m =
    m.DummyRoot
    |> RoseTree.flatten
    |> List.find (fun n -> n.Data.Id = pid)
    |> RoseTree.dataOfChildren

  /// Returns the parent of the specified child counter ID.
  let parentOf cid m =
    m.DummyRoot
    |> RoseTree.flatten
    |> List.find (RoseTree.dataOfChildren >> List.map Identifiable.getId >> List.contains cid)

  /// Returns the sibling counters of the specified counter ID.
  let childrenCountersOfParentOf cid =
    cid |> parentOf >> RoseTree.dataOfChildren


module Bindings =

  open App

  let rec counterTreeBindings () : Binding<Model * Identifiable<Counter>, Msg> list =
    let counterBindings =
      Counter.bindings ()
      |> Bindings.mapModel (fun (_, c) -> c.Value)
      |> Bindings.mapMsgWithModel (fun (_, c) msg -> CounterMsg (c.Id, msg))
    let newBindings =
      [
        "CounterIdText" |> Binding.oneWay(fun (_, c) -> c.Id)
      
        "Remove" |> Binding.cmd(fun (_, c) -> Remove c.Id)
      
        "AddChild" |> Binding.cmd(fun (_, c) -> AddChild c.Id)
      
        "MoveUp" |> Binding.cmdIf(
          (fun (_, c) -> MoveUp c.Id),
          (fun (m, c) -> m |> childrenCountersOfParentOf c.Id |> List.tryHead <> Some c))
      
        "MoveDown" |> Binding.cmdIf(
          (fun (_, c) -> MoveDown c.Id),
          (fun (m, c) -> m |> childrenCountersOfParentOf c.Id |> List.tryLast <> Some c))
      
        "GlobalState" |> Binding.oneWay(fun (m, _) -> m.SomeGlobalState)
      
        "ChildCounters" |> Binding.subModelSeq(
          (fun (m, c) -> m |> childCountersOf c.Id),
          (fun ((m, _), childCounter) -> (m, childCounter)),
          (fun (_, c) -> c.Id),
          snd,
          counterTreeBindings)
      ]
    [ counterBindings
      newBindings ]
    |> List.concat


  let rootBindings () : Binding<Model, Msg> list = [
    "Counters" |> Binding.subModelSeq(
      (fun m -> m |> topLevelCounters),
      (fun c -> c.Id),
      counterTreeBindings)

    "ToggleGlobalState" |> Binding.cmd ToggleGlobalState

    "AddCounter" |> Binding.cmd(fun m -> AddChild m.DummyRoot.Data.Id)
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
  |> WpfProgram.runWindow window
