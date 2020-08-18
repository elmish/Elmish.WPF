module Elmish.WPF.Samples.SubModelSeq.Program

open System
open Elmish
open Elmish.WPF


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
      | [] -> input
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

    let branchMsg a t = BranchMsg (a, t)

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


module Bindings =

  open App

  type SelfWithParent<'a> =
    { Self: 'a
      Parent: 'a }

  let canMoveUp (_, { Parent = p; Self = s }) =
    match p.Children |> List.tryHead with
    | Some c when c.Data.Id <> s.Data.Id ->
        s.Data.Id |> MoveUp |> Some
    | _ -> None

  let canMoveDown (_, { Parent = p; Self = s }) =
    match p.Children |> List.tryLast with
    | Some c when c.Data.Id <> s.Data.Id ->
        s.Data.Id |> MoveDown |> Some
    | _ -> None
    
  let adjustMsgToParent msg =
    match msg with
    | BranchMsg (pId, LeafMsg (Remove   cId)) when pId = cId -> LeafMsg (Remove cId)
    | BranchMsg (pId, LeafMsg (MoveUp   cId)) when pId = cId -> LeafMsg (MoveUp cId)
    | BranchMsg (pId, LeafMsg (MoveDown cId)) when pId = cId -> LeafMsg (MoveDown cId)
    | _ -> msg

  let rec subtreeBindings () : Binding<Model * SelfWithParent<RoseTree<Identifiable<Counter>>>, RoseTreeMsg<Guid, SubtreeMsg>> list = [
    "CounterIdText" |> Binding.oneWay(fun (_, { Self = s }) -> s.Data.Id)

    "CounterValue" |> Binding.oneWay(fun (_, { Self = s }) -> s.Data.Value.Count)
    "Increment" |> Binding.cmd(Increment |> CounterMsg |> LeafMsg)
    "Decrement" |> Binding.cmd(Decrement |> CounterMsg |> LeafMsg)
    "StepSize" |> Binding.twoWay(
      (fun (_, { Self = s }) -> float s.Data.Value.StepSize),
      (fun v _ -> v |> int |> SetStepSize |> CounterMsg |> LeafMsg))
    "Reset" |> Binding.cmdIf(
      Reset |> CounterMsg |> LeafMsg,
      (fun (_, { Self = s }) -> Counter.canReset s.Data.Value))

    "Remove" |> Binding.cmd(fun (_, { Self = s }) -> s.Data.Id |> Remove |> LeafMsg)
    "AddChild" |> Binding.cmd(AddChild |> LeafMsg)

    "MoveUp" |> Binding.cmdIf(canMoveUp |> FuncOption.map LeafMsg)
    "MoveDown" |> Binding.cmdIf(canMoveDown |> FuncOption.map LeafMsg)

    "GlobalState" |> Binding.oneWay(fun (m, _) -> m.SomeGlobalState)

    "ChildCounters" |> Binding.subModelSeq(
      (fun (_, { Self = p }) -> p.Children |> Seq.map (fun c -> { Self = c; Parent = p })),
      (fun ((m, _), selfAndParent) -> (m, selfAndParent)),
      (fun (_, { Self = c }) -> c.Data.Id),
      (fun (id, msg) -> msg |> RoseTree.branchMsg id |> adjustMsgToParent),
      subtreeBindings)
  ]

  let rootBindings () : Binding<Model, Msg> list = [
    "Counters" |> Binding.subModelSeq(
      (fun m -> m.DummyRoot.Children |> Seq.map (fun c -> { Self = c; Parent = m.DummyRoot })),
      (fun { Self = c } -> c.Data.Id),
      (fun (id, msg) -> msg |> RoseTree.branchMsg id |> adjustMsgToParent |> SubtreeMsg),
      subtreeBindings)

    "ToggleGlobalState" |> Binding.cmd ToggleGlobalState

    "AddCounter" |> Binding.cmd (AddChild |> LeafMsg |> SubtreeMsg)
  ]


let mainDesignVm = ViewModel.designInstance (App.init ()) (Bindings.rootBindings ())

let main window =
  Program.mkSimpleWpf App.init App.update Bindings.rootBindings
  |> Program.withConsoleTrace
  |> Program.runWindowWithConfig
    { ElmConfig.Default with LogConsole = true; Measure = true }
    window
