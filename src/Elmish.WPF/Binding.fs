namespace Elmish.WPF

open System.Collections.Generic
open System.Collections.ObjectModel
open System.Windows

open Elmish



[<AutoOpen>]
module internal BindingLogic =


  let elmStyleMerge
        getSourceId
        getTargetId
        create
        update
        (target: ObservableCollection<_>)
        (source: _ array) =
    (*
     * Based on Elm's HTML.keyed
     * https://guide.elm-lang.org/optimization/keyed.html
     * https://github.com/elm/virtual-dom/blob/5a5bcf48720bc7d53461b3cd42a9f19f119c5503/src/Elm/Kernel/VirtualDom.js#L980-L1226
     *)
    let removals = Dictionary<_, _> ()
    let additions = Dictionary<_, _> ()

    let recordRemoval curTargetIdx curTarget curTargetId =
      removals.Add(curTargetId, (curTargetIdx, curTarget))
    let recordAddition curSourceIdx curSource curSourceId =
      additions.Add(curSourceId, (curSourceIdx, curSource))

    let mutable curSourceIdx = 0
    let mutable curTargetIdx = 0

    let mutable shouldContinue = true

    let sourceCount = source.Length
    let targetCount = target.Count

    while (shouldContinue && curSourceIdx < sourceCount && curTargetIdx < targetCount) do
      let curSource = source.[curSourceIdx]
      let curTarget = target.[curTargetIdx]

      let curSourceId = getSourceId curSource
      let curTargetId = getTargetId curTarget

      if curSourceId = curTargetId then
        update curTarget curSource curTargetIdx

        curSourceIdx <- curSourceIdx + 1
        curTargetIdx <- curTargetIdx + 1
      else
        let mNextSource =
          source
          |> Array.tryItem (curSourceIdx + 1)
          |> Option.map (fun s ->
            let id = getSourceId s
            s, id, id = curTargetId) // true => need to add

        let mNextTarget =
          if curTargetIdx + 1 < targetCount then target.[curTargetIdx + 1] |> Some else None
          |> Option.map (fun t ->
            let id = getTargetId t
            t, id, id = curSourceId) // true => need to remove

        match mNextSource, mNextTarget with
        | Some (nextSource, _, true), Some (nextTarget, _, true) -> // swap adjacent
            target.[curTargetIdx] <- nextTarget
            target.[curTargetIdx + 1] <- curTarget

            update curTarget nextSource (curTargetIdx + 1)
            update nextTarget curSource curTargetIdx
            
            curSourceIdx <- curSourceIdx + 2
            curTargetIdx <- curTargetIdx + 2
        |               None, Some (nextTarget, _, true)
        | Some (_, _, false), Some (nextTarget, _, true) -> // remove
            recordRemoval curTargetIdx curTarget curTargetId
            
            update nextTarget curSource curTargetIdx

            curSourceIdx <- curSourceIdx + 1
            curTargetIdx <- curTargetIdx + 2
        | Some (nextSource, _, true), None
        | Some (nextSource, _, true), Some (_, _, false) -> // add
            recordAddition curSourceIdx curSource curSourceId
            
            update curTarget nextSource (curTargetIdx + 1)
            
            curSourceIdx <- curSourceIdx + 2
            curTargetIdx <- curTargetIdx + 1
        | Some (_, _, false),               None
        |               None, Some (_, _, false)
        |               None,               None -> // source and target have different lengths and we have reached the end of one
            shouldContinue <- false 
        | Some (nextSource, nextSourceId, false), Some (nextTarget, nextTargetId, false) ->
            if nextSourceId = nextTargetId then // replace
              recordRemoval curTargetIdx curTarget curTargetId
              recordAddition curSourceIdx curSource curSourceId
              
              update nextTarget nextSource (curTargetIdx + 1)

              curSourceIdx <- curSourceIdx + 2
              curTargetIdx <- curTargetIdx + 2
            else // collections very different
              shouldContinue <- false

    // replace many
    while (curSourceIdx < sourceCount && curTargetIdx < targetCount) do
      let curSource = source.[curSourceIdx]
      let curTarget = target.[curTargetIdx]

      let curSourceId = getSourceId curSource
      let curTargetId = getTargetId curTarget

      recordRemoval curTargetIdx curTarget curTargetId
      recordAddition curSourceIdx curSource curSourceId
      
      curSourceIdx <- curSourceIdx + 1
      curTargetIdx <- curTargetIdx + 1

    // remove many
    for i in targetCount - 1..-1..curTargetIdx do
      let t = target.[i]
      let id = getTargetId t
      recordRemoval i t id

    // add many
    for i in curSourceIdx..sourceCount - 1 do
      let s = source.[i]
      let id = getSourceId s
      recordAddition i s id

    let moves =
      additions
      |> Seq.toList
      |> List.collect (fun (Kvp (id, (sIdx, s))) ->
        removals
        |> Dictionary.tryFind id
        |> Option.map (fun (tIdx, t) ->
            removals.Remove id |> ignore
            additions.Remove id |> ignore
            (tIdx, sIdx, t, s) |> List.singleton)
        |> Option.defaultValue [])

    let actuallyRemove () =
      Seq.empty
      |> Seq.append (removals |> Seq.map (Kvp.value >> fst))
      |> Seq.append (moves |> Seq.map (fun (tIdx, _, _, _) -> tIdx))
      |> Seq.sortDescending // so we remove by index from largest to smallest
      |> Seq.iter target.RemoveAt

    let actuallyAdd () =
      Seq.empty
      |> Seq.append (additions |> Seq.map (fun (Kvp (id, (idx, s))) -> idx, create s id))
      |> Seq.append (moves |> Seq.map (fun (_, sIdx, t, _) -> sIdx, t))
      |> Seq.sortBy fst // so we insert by index from smallest to largest
      |> Seq.iter target.Insert

    match moves, removals.Count, additions.Count with
    | [ (tIdx, sIdx, _, _) ], 0, 0 -> // single move
        target.Move(tIdx, sIdx)
    | [ (t1Idx, s1Idx, _, _); (t2Idx, s2Idx, _, _) ], 0, 0 when t1Idx = s2Idx && t2Idx = s1Idx-> // single swap
        let temp = target.[t1Idx]
        target.[t1Idx] <- target.[t2Idx]
        target.[t2Idx] <- temp
    | _, rc, _ when rc = targetCount && rc > 0 -> // remove everything (implies moves = [])
        target.Clear ()
        actuallyAdd ()
    | _ ->
        actuallyRemove ()
        actuallyAdd ()

    // update moved elements
    moves |> Seq.iter (fun (_, sIdx, t, s) -> update t s sIdx)


[<RequireQualifiedAccess>]
type WindowState<'model> =
  | Closed
  | Hidden of 'model
  | Visible of 'model

module WindowState =

  let map f state =
    match state with
    | WindowState.Closed -> WindowState.Closed
    | WindowState.Hidden a -> WindowState.Hidden (f a)
    | WindowState.Visible a -> WindowState.Visible (f a)

  let toVOption state =
    match state with
    | WindowState.Closed -> ValueNone
    | WindowState.Hidden a -> ValueSome a
    | WindowState.Visible a -> ValueSome a

  /// Converts None to WindowState.Closed, and Some(x) to
  /// WindowState.Visible(x).
  let ofOption (model: 'model option) =
    match model with
    | Some x -> WindowState.Visible x
    | None -> WindowState.Closed

  /// Converts ValueNone to WindowState.Closed, and ValueSome(x) to
  /// WindowState.Visible(x).
  let ofVOption (model: 'model voption) =
    match model with
    | ValueSome x -> WindowState.Visible x
    | ValueNone -> WindowState.Closed


type internal OneWayData<'model, 'a when 'a : equality> =
  { Get: 'model -> 'a }

  member d.DidPropertyChange((currentModel: 'model), (newModel: 'model)) =
    d.Get currentModel <> d.Get newModel

  member d.TryGetMember(model: 'model) =
    d.Get model


type internal OneWayLazyData<'model, 'a, 'b> =
  { Get: 'model -> 'a
    Map: 'a -> 'b
    Equals: 'a -> 'a -> bool }
    
  member d.DidProeprtyChange((currentModel: 'model), (newModel: 'model)) =
    not <| d.Equals (d.Get newModel) (d.Get currentModel)

  member d.TryGetMember(model: 'model) =
    model |> d.Get |> d.Map


type internal OneWaySeqLazyData<'model, 'a, 'b, 'id when 'id : equality> =
  { Get: 'model -> 'a
    Map: 'a -> 'b seq
    Equals: 'a -> 'a -> bool
    GetId: 'b -> 'id
    ItemEquals: 'b -> 'b -> bool }
    
  member d.Merge((values: ObservableCollection<'b>), (currentModel: 'model), (newModel: 'model)) =
    let intermediate = d.Get newModel
    if not <| d.Equals intermediate (d.Get currentModel) then
      let create v _ = v
      let update oldVal newVal oldIdx =
        if not (d.ItemEquals newVal oldVal) then
          values.[oldIdx] <- newVal
      let newVals = intermediate |> d.Map |> Seq.toArray
      elmStyleMerge d.GetId d.GetId create update values newVals


type internal TwoWayData<'model, 'msg, 'a when 'a : equality> =
  { Get: 'model -> 'a
    Set: 'a -> 'model -> 'msg }
    
  member d.DidPropertyChange((currentModel: 'model), (newModel: 'model)) =
    d.Get currentModel <> d.Get newModel

  member d.TryGetMember(model: 'model) =
    d.Get model

  member d.TrySetMember((value: 'a), (model: 'model)) =
    d.Set value model


type internal TwoWayValidateData<'model, 'msg, 'a when 'a : equality> =
  { Get: 'model -> 'a
    Set: 'a -> 'model -> 'msg
    Validate: 'model -> string list }
    
  member d.DidPropertyChange((currentModel: 'model), (newModel: 'model)) =
    d.Get currentModel <> d.Get newModel

  member d.TryGetMember(model: 'model) =
    d.Get model

  member d.TrySetMember((value: 'a), (model: 'model)) =
    d.Set value model


type internal CmdData<'model, 'msg> = {
  Exec: 'model -> 'msg voption
  CanExec: 'model -> bool
}


type internal CmdParamData<'model, 'msg> = {
  Exec: obj -> 'model -> 'msg voption
  CanExec: obj -> 'model -> bool
  AutoRequery: bool
}


type internal SubModelSelectedItemData<'model, 'msg, 'id when 'id : equality> =
  { Get: 'model -> 'id voption
    Set: 'id voption -> 'model -> 'msg
    SubModelSeqBindingName: string }
    
  member d.DidPropertyChange((currentModel: 'model), (newModel: 'model)) =
    d.Get currentModel <> d.Get newModel

  member d.TryGetMember
      ((getBindingModel: 'b -> 'bindingModel),
       (subModelSeqData: SubModelSeqData<'model, 'msg, 'bindingModel, 'bindingMsg, 'id>),
       (viewModels: ObservableCollection<'b>),
       (model: 'model)) =
    let selectedId = d.Get model
    viewModels
    |> Seq.tryFind (getBindingModel >> subModelSeqData.GetId >> ValueSome >> (=) selectedId)

  member d.TrySetMember
      ((subModelSeqData: SubModelSeqData<'model, 'msg, 'bindingModel, 'bindingMsg, 'id>),
       (model: 'model),
       (bindingModel: 'bindingModel voption)) =
    let id = bindingModel |> ValueOption.map subModelSeqData.GetId
    d.Set id model


and internal SubModelData<'model, 'msg, 'bindingModel, 'bindingMsg> = {
  GetModel: 'model -> 'bindingModel voption
  GetBindings: unit -> Binding<'bindingModel, 'bindingMsg> list
  ToMsg: 'model -> 'bindingMsg -> 'msg
  Sticky: bool
}


and internal SubModelWinData<'model, 'msg, 'bindingModel, 'bindingMsg> = {
  GetState: 'model -> WindowState<'bindingModel>
  GetBindings: unit -> Binding<'bindingModel, 'bindingMsg> list
  ToMsg: 'model -> 'bindingMsg -> 'msg
  GetWindow: 'model -> Dispatch<'msg> -> Window
  IsModal: bool
  OnCloseRequested: 'model -> 'msg voption
}


and internal SubModelSeqData<'model, 'msg, 'bindingModel, 'bindingMsg, 'id when 'id : equality> =
  { GetModels: 'model -> 'bindingModel seq
    GetId: 'bindingModel -> 'id
    GetBindings: unit -> Binding<'bindingModel, 'bindingMsg> list
    ToMsg: 'model -> 'id * 'bindingMsg -> 'msg }
    
  member d.Merge
      ((getTargetId: ('bindingModel -> 'id) -> 'b -> 'id),
       (create: 'bindingModel -> 'id -> 'b),
       (update: 'b -> 'bindingModel -> Unit),
       (values: ObservableCollection<'b>),
       (newModel: 'model)) =
    let update b bm _ = update b bm
    let newSubModels = newModel |> d.GetModels |> Seq.toArray
    elmStyleMerge d.GetId (getTargetId d.GetId) create update values newSubModels


/// Represents all necessary data used to create the different binding types.
and internal BindingData<'model, 'msg> =
  | OneWayData of OneWayData<'model, obj>
  | OneWayLazyData of OneWayLazyData<'model, obj, obj>
  | OneWaySeqLazyData of OneWaySeqLazyData<'model, obj, obj, obj>
  | TwoWayData of TwoWayData<'model, 'msg, obj>
  | TwoWayValidateData of TwoWayValidateData<'model, 'msg, obj>
  | CmdData of CmdData<'model, 'msg>
  | CmdParamData of CmdParamData<'model, 'msg>
  | SubModelData of SubModelData<'model, 'msg, obj, obj>
  | SubModelWinData of SubModelWinData<'model, 'msg, obj, obj>
  | SubModelSeqData of SubModelSeqData<'model, 'msg, obj, obj, obj>
  | SubModelSelectedItemData of SubModelSelectedItemData<'model, 'msg, obj>


/// Represents all necessary data used to create a binding.
and Binding<'model, 'msg> =
  internal
    { Name: string
      Data: BindingData<'model, 'msg> }


module internal BindingData =

  let subModelSelectedItemLast a b =
    match a, b with
    | SubModelSelectedItemData _, SubModelSelectedItemData _ -> 0
    | SubModelSelectedItemData _, _ -> 1
    | _, SubModelSelectedItemData _ -> -1
    | _, _ -> 0

  let mapModel f =
    let binaryHelper binary x m = (x, f m) ||> binary
    let mapModelRec = function
      | OneWayData d -> OneWayData {
          Get = f >> d.Get
        }
      | OneWayLazyData d -> OneWayLazyData  {
          Get = f >> d.Get
          Map = d.Map;
          Equals = d.Equals
        }
      | OneWaySeqLazyData d -> OneWaySeqLazyData {
          Get = f >> d.Get
          Map = d.Map
          Equals = d.Equals
          GetId = d.GetId
          ItemEquals = d.ItemEquals
        }
      | TwoWayData d -> TwoWayData {
          Get = f >> d.Get
          Set = binaryHelper d.Set
        }
      | TwoWayValidateData d -> TwoWayValidateData {
          Get = f >> d.Get
          Set = binaryHelper d.Set
          Validate = f >> d.Validate
        }
      | CmdData d -> CmdData {
          Exec = f >> d.Exec
          CanExec = f >> d.CanExec
        }
      | CmdParamData d -> CmdParamData {
          Exec = binaryHelper d.Exec
          CanExec = binaryHelper d.CanExec
          AutoRequery = d.AutoRequery
        }
      | SubModelData d -> SubModelData {
          GetModel = f >> d.GetModel
          GetBindings = d.GetBindings
          ToMsg = f >> d.ToMsg
          Sticky = d.Sticky
        }
      | SubModelWinData d -> SubModelWinData {
          GetState = f >> d.GetState
          GetBindings = d.GetBindings
          ToMsg = f >> d.ToMsg
          GetWindow = f >> d.GetWindow
          IsModal = d.IsModal
          OnCloseRequested = f >> d.OnCloseRequested
        }
      | SubModelSeqData d -> SubModelSeqData {
          GetModels = f >> d.GetModels
          GetId = d.GetId
          GetBindings = d.GetBindings
          ToMsg = f >> d.ToMsg
        }
      | SubModelSelectedItemData d -> SubModelSelectedItemData {
          Get = f >> d.Get
          Set = binaryHelper d.Set
          SubModelSeqBindingName = d.SubModelSeqBindingName
        }
    mapModelRec

  let mapMsgWithModel f =
    let mapMsgWithModelRec = function
      | OneWayData d -> d |> OneWayData
      | OneWayLazyData d -> d |> OneWayLazyData
      | OneWaySeqLazyData d -> d |> OneWaySeqLazyData
      | TwoWayData d -> TwoWayData {
          Get = d.Get
          Set = fun v m -> d.Set v m |> f m
        }
      | TwoWayValidateData d -> TwoWayValidateData {
          Get = d.Get
          Set = fun v m -> d.Set v m |> f m
          Validate = unbox >> d.Validate
        }
      | CmdData d -> CmdData {
          Exec = fun m -> m |> d.Exec |> ValueOption.map (f m)
          CanExec = d.CanExec
        }
      | CmdParamData d -> CmdParamData {
          Exec = fun p m -> d.Exec p m |> ValueOption.map (f m)
          CanExec = fun p m -> d.CanExec p m
          AutoRequery = d.AutoRequery
        }
      | SubModelData d -> SubModelData {
          GetModel = d.GetModel
          GetBindings = d.GetBindings
          ToMsg = fun m x -> (m, x) ||> d.ToMsg |> f m
          Sticky = d.Sticky
        }
      | SubModelWinData d -> SubModelWinData {
          GetState = d.GetState
          GetBindings = d.GetBindings
          ToMsg = fun m x -> (m, x) ||> d.ToMsg |> f m
          GetWindow = fun m dispatch -> d.GetWindow m (m |> f >> dispatch)
          IsModal = d.IsModal
          OnCloseRequested = fun m -> m |> d.OnCloseRequested |> ValueOption.map (f m)
        }
      | SubModelSeqData d -> SubModelSeqData {
          GetModels = d.GetModels
          GetId = d.GetId
          GetBindings = d.GetBindings
          ToMsg = fun m x -> (m, x) ||> d.ToMsg |> f m
        }
      | SubModelSelectedItemData d -> SubModelSelectedItemData {
          Get = d.Get
          Set = fun v m -> d.Set v m |> f m
          SubModelSeqBindingName = d.SubModelSeqBindingName
        }
    mapMsgWithModelRec

  let mapMsg f = mapMsgWithModel (fun _ -> f)


module Binding =

  let internal mapData f binding =
    { Name = binding.Name
      Data = binding.Data |> f }

  /// Map the model type parameter of a binding via a contravariant mapping.
  let mapModel (f: 'a -> 'b) (binding: Binding<'b, 'msg>) = binding |> mapData (BindingData.mapModel f)
  
  /// Map the message type parameter of a binding with access to the model via a covariant mapping.
  let mapMsgWithModel (f: 'model -> 'a -> 'b) (binding: Binding<'model, 'a>) = binding |> mapData (BindingData.mapMsgWithModel f)
  
  /// Map the message type parameter of a binding via a covariant mapping.
  let mapMsg (f: 'a -> 'b) (binding: Binding<'model, 'a>) = binding |> mapData (BindingData.mapMsg f)

  let internal subModelSelectedItemLast a b =
    BindingData.subModelSelectedItemLast a.Data b.Data

  /// Restrict the binding to models that satisfy the predicate after some model satisfies the predicate.
  let sticky (predicate: 'model -> bool) (binding: Binding<'model, 'msg>) =
    let mutable stickyModel = None
    let f newModel =
      match predicate newModel, stickyModel with
      | false, Some sm ->
          sm
      | false, None ->
          newModel
      | true, _ ->
          stickyModel <- Some newModel
          newModel
    binding |> mapModel f


module Bindings =

  /// Map the model type parameter of a list of bindings via a contravariant mapping.
  let mapModel (f: 'a -> 'b) (bindings: Binding<'b, 'msg> list) = bindings |> List.map (Binding.mapModel f)
  
  /// Map the message type parameter of a list of bindings with access to the model via a covariant mapping.
  let mapMsgWithModel (f: 'model -> 'a -> 'b) (bindings: Binding<'model, 'a> list) = bindings |> List.map (Binding.mapMsgWithModel f)
  
  /// Map the message type parameter of a list of bindings via a covariant mapping.
  let mapMsg (f: 'a -> 'b) (bindings: Binding<'model, 'a> list) = bindings |> List.map (Binding.mapMsg f)



[<AutoOpen>]
module internal BindingData2 =

  module Option =

    let box ma = ma |> Option.map box |> Option.toObj
    let unbox obj = obj |> Option.ofObj |> Option.map unbox

  module ValueOption =

    let box ma = ma |> ValueOption.map box |> ValueOption.toObj
    let unbox obj = obj |> ValueOption.ofObj |> ValueOption.map unbox


  module OneWayData =
  
    let mapMinorTypes
        (outMapA: 'a -> 'a0)
        (d: OneWayData<'model, 'a>) = {
      Get = d.Get >> outMapA
    }

    let boxVOpt d = mapMinorTypes ValueOption.box d
    let boxOpt d = mapMinorTypes Option.box d
    let box d = mapMinorTypes box d

    let mapFunctions
        mGet
        (d: OneWayData<'model, 'a>) =
      { d with Get = mGet d.Get }

    let measureFunctions
        mGet =
      mapFunctions
        (mGet "get")


  module OneWayLazyData =
  
    let mapMinorTypes
        (outMapA: 'a -> 'a0)
        (outMapB: 'b -> 'b0)
        (inMapA: 'a0 -> 'a)
        (d: OneWayLazyData<'model, 'a, 'b>) = {
      Get = d.Get >> outMapA
      Map = inMapA >> d.Map >> outMapB
      Equals = fun a1 a2 -> d.Equals (inMapA a1) (inMapA a2)
    }

    let boxVOpt d = mapMinorTypes box ValueOption.box unbox d
    let boxOpt d = mapMinorTypes box Option.box unbox d
    let box d = mapMinorTypes box box unbox d

    let mapFunctions
        mGet
        mMap
        mEquals
        (d: OneWayLazyData<'model, 'a, 'b>) =
      { d with Get = mGet d.Get
               Map = mMap d.Map
               Equals = mEquals d.Equals }

    let measureFunctions
        mGet
        mMap
        mEquals =
      mapFunctions
        (mGet "get")
        (mMap "map")
        (mEquals "equals")


  module OneWaySeqLazyData =
  
    let mapMinorTypes
        (outMapA: 'a -> 'a0)
        (outMapB: 'b -> 'b0)
        (outMapId: 'id -> 'id0)
        (inMapA: 'a0 -> 'a)
        (inMapB: 'b0 -> 'b)
        (d: OneWaySeqLazyData<'model, 'a, 'b, 'id>) = {
      Get = d.Get >> outMapA
      Map = inMapA >> d.Map >> Seq.map outMapB
      Equals = fun a1 a2 -> d.Equals (inMapA a1) (inMapA a2)
      GetId = inMapB >> d.GetId >> outMapId
      ItemEquals = fun b1 b2 -> d.ItemEquals (inMapB b1) (inMapB b2)
    }

    let box d = mapMinorTypes box box box unbox unbox d

    let mapFunctions
        mGet
        mMap
        mEquals
        mGetId
        mItemEquals
        (d: OneWaySeqLazyData<'model, 'a, 'b, 'id>) =
      { d with Get = mGet d.Get
               Map = mMap d.Map
               Equals = mEquals d.Equals
               GetId = mGetId d.GetId
               ItemEquals = mItemEquals d.ItemEquals }

    let measureFunctions
        mGet
        mMap
        mEquals
        mGetId
        mItemEquals =
      mapFunctions
        (mGet "get")
        (mMap "map")
        (mEquals "equals")
        (mGetId "getId")
        (mItemEquals "itemEquals")


  module TwoWayData =
  
    let mapMinorTypes
        (outMapA: 'a -> 'a0)
        (inMapA: 'a0 -> 'a)
        (d: TwoWayData<'model, 'msg, 'a>) = {
      Get = d.Get >> outMapA
      Set = fun a m -> d.Set (inMapA a) m
    }

    let boxVOpt d = mapMinorTypes ValueOption.box ValueOption.unbox d
    let boxOpt d = mapMinorTypes Option.box Option.unbox d
    let box d = mapMinorTypes box unbox d

    let mapFunctions
        mGet
        mSet
        (d: TwoWayData<'model, 'msg, 'a>) =
      { d with Get = mGet d.Get
               Set = mSet d.Set }

    let measureFunctions
        mGet
        mSet =
      mapFunctions
        (mGet "get")
        (mSet "set")


  module TwoWayValidateData =
  
    let mapMinorTypes
        (outMapA: 'a -> 'a0)
        (inMapA: 'a0 -> 'a)
        (d: TwoWayValidateData<'model, 'msg, 'a>) = {
      Get = d.Get >> outMapA
      Set = fun a m -> d.Set (inMapA a) m
      Validate = d.Validate
    }

    let boxVOpt d = mapMinorTypes ValueOption.box ValueOption.unbox d
    let boxOpt d = mapMinorTypes Option.box Option.unbox d
    let box d = mapMinorTypes box unbox d

    let mapFunctions
        mGet
        mSet
        mValidate
        (d: TwoWayValidateData<'model, 'msg, 'a>) =
      { d with Get = mGet d.Get
               Set = mSet d.Set
               Validate = mValidate d.Validate }

    let measureFunctions
        mGet
        mSet
        mValidate =
      mapFunctions
        (mGet "get")
        (mSet "set")
        (mValidate "validate")


  module CmdData =

    let mapFunctions
        mExec
        mCanExec
        (d: CmdData<'model, 'msg>) =
      { d with Exec = mExec d.Exec
               CanExec = mCanExec d.CanExec }

    let measureFunctions
        mExec
        mCanExec =
      mapFunctions
        (mExec "exec")
        (mCanExec "canExec")


  module CmdParamData =

    let mapFunctions
        mExec
        mCanExec
        (d: CmdParamData<'model, 'msg>) =
      { d with Exec = mExec d.Exec
               CanExec = mCanExec d.CanExec }

    let measureFunctions
        mExec
        mCanExec =
      mapFunctions
        (mExec "exec")
        (mCanExec "canExec")


  module SubModelSelectedItemData =
  
    let mapMinorTypes
        (outMapId: 'id -> 'id0)
        (inMapId: 'id0 -> 'id)
        (d: SubModelSelectedItemData<'model, 'msg, 'id>) = {
      Get = d.Get >> ValueOption.map outMapId
      Set = ValueOption.map inMapId >> d.Set
      SubModelSeqBindingName = d.SubModelSeqBindingName
    }

    let box d = mapMinorTypes box unbox d

    let mapFunctions
        mGet
        mSet
        (d: SubModelSelectedItemData<'model, 'msg, 'id>) =
      { d with Get = mGet d.Get
               Set = mSet d.Set }

    let measureFunctions
        mGet
        mSet =
      mapFunctions
        (mGet "get")
        (mSet "set")


  module SubModelData =
  
    let mapMinorTypes
        (outMapBindingModel: 'bindingModel -> 'bindingModel0)
        (outMapBindingMsg: 'bindingMsg -> 'bindingMsg0)
        (inMapBindingModel: 'bindingModel0 -> 'bindingModel)
        (inMapBindingMsg: 'bindingMsg0 -> 'bindingMsg)
        (d: SubModelData<'model, 'msg, 'bindingModel, 'bindingMsg>) = {
      GetModel = d.GetModel >> ValueOption.map outMapBindingModel
      GetBindings = d.GetBindings >> Bindings.mapModel inMapBindingModel >> Bindings.mapMsg outMapBindingMsg
      ToMsg = fun m bMsg -> d.ToMsg m (inMapBindingMsg bMsg)
      Sticky = d.Sticky
    }

    let box d = mapMinorTypes box box unbox unbox d

    let mapFunctions
        mGetModel
        mGetBindings
        mToMsg
        (d: SubModelData<'model, 'msg, 'bindingModel, 'bindingMsg>) =
      { d with GetModel = mGetModel d.GetModel
               GetBindings = mGetBindings d.GetBindings
               ToMsg = mToMsg d.ToMsg }

    let measureFunctions
        mGetModel
        mGetBindings
        mToMsg =
      mapFunctions
        (mGetModel "getSubModel") // sic: "getModel" would be following the pattern
        (mGetBindings "bindings") // sic: "getBindings" would be following the pattern
        (mToMsg "toMsg")


  module SubModelWinData =
  
    let mapMinorTypes
        (outMapBindingModel: 'bindingModel -> 'bindingModel0)
        (outMapBindingMsg: 'bindingMsg -> 'bindingMsg0)
        (inMapBindingModel: 'bindingModel0 -> 'bindingModel)
        (inMapBindingMsg: 'bindingMsg0 -> 'bindingMsg)
        (d: SubModelWinData<'model, 'msg, 'bindingModel, 'bindingMsg>) = {
      GetState = d.GetState >> WindowState.map outMapBindingModel
      GetBindings = d.GetBindings >> Bindings.mapModel inMapBindingModel >> Bindings.mapMsg outMapBindingMsg
      ToMsg = fun m bMsg -> d.ToMsg m (inMapBindingMsg bMsg)
      GetWindow = d.GetWindow
      IsModal = d.IsModal
      OnCloseRequested = d.OnCloseRequested
    }

    let box d = mapMinorTypes box box unbox unbox d

    let mapFunctions
        mGetState
        mGetBindings
        mToMsg
        mGetWindow
        mOnCloseRequested
        (d: SubModelWinData<'model, 'msg, 'bindingModel, 'bindingMsg>) =
      { d with GetState = mGetState d.GetState
               GetBindings = mGetBindings d.GetBindings
               ToMsg = mToMsg d.ToMsg
               GetWindow = mGetWindow d.GetWindow
               OnCloseRequested = mOnCloseRequested d.OnCloseRequested }

    let measureFunctions
        mGetState
        mGetBindings
        mToMsg =
      mapFunctions
        (mGetState "getState")
        (mGetBindings "bindings") // sic: "getBindings" would be following the pattern
        (mToMsg "toMsg")
        id // sic: could measure GetWindow
        id // sic: could measure OnCloseRequested


  module SubModelSeqData =
  
    let mapMinorTypes
        (outMapBindingModel: 'bindingModel -> 'bindingModel0)
        (outMapBindingMsg: 'bindingMsg -> 'bindingMsg0)
        (outMapId: 'id -> 'id0)
        (inMapBindingModel: 'bindingModel0 -> 'bindingModel)
        (inMapBindingMsg: 'bindingMsg0 -> 'bindingMsg)
        (inMapId: 'id0 -> 'id)
        (d: SubModelSeqData<'model, 'msg, 'bindingModel, 'bindingMsg, 'id>) = {
      GetModels = d.GetModels >> Seq.map outMapBindingModel
      GetId = inMapBindingModel >> d.GetId >> outMapId
      GetBindings = d.GetBindings >> Bindings.mapModel inMapBindingModel >> Bindings.mapMsg outMapBindingMsg
      ToMsg = fun m (id, bMsg) -> d.ToMsg m ((inMapId id), (inMapBindingMsg bMsg))
    }

    let box d = mapMinorTypes box box box unbox unbox unbox d

    let mapFunctions
        mGetModels
        mGetId
        mGetBindings
        mToMsg
        (d: SubModelSeqData<'model, 'msg, 'bindingModel, 'bindingMsg, 'id>) =
      { d with GetModels = mGetModels d.GetModels
               GetId = mGetId d.GetId
               GetBindings = mGetBindings d.GetBindings
               ToMsg = mToMsg d.ToMsg }

    let measureFunctions
        mGetModels
        mGetId
        mGetBindings
        mToMsg =
      mapFunctions
        (mGetModels "getSubModels") // sic: "getModels" would follow the pattern
        (mGetId "getId")
        (mGetBindings "bindings") // sic: "getBindings" would follow the pattern
        (mToMsg "toMsg")




[<AutoOpen>]
module internal Helpers =

  let createBinding data name =
    { Name = name
      Data = data }



[<AbstractClass; Sealed>]
type Binding private () =

  ///<summary>Creates a one-way binding with the model as the value.</summary>
  static member internal id () : string -> Binding<'model, 'msg> =
    { Get = id }
    |> OneWayData.box
    |> OneWayData
    |> createBinding


  /// <summary>Creates a one-way binding.</summary>
  /// <param name="get">Gets the value from the model.</param>
  static member oneWay
      (get: 'model -> 'a)
      : string -> Binding<'model, 'msg> =
    { Get = get }
    |> OneWayData.box
    |> OneWayData
    |> createBinding


  /// <summary>
  ///   Creates a one-way binding to an optional value. The binding
  ///   automatically converts between the optional source value and an
  ///   unwrapped (possibly
  ///   <c>null</c>) value on the view side.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  static member oneWayOpt
      (get: 'model -> 'a option)
      : string -> Binding<'model, 'msg> =
    { Get = get }
    |> OneWayData.boxOpt
    |> OneWayData
    |> createBinding


  /// <summary>
  ///   Creates a one-way binding to an optional value. The binding
  ///   automatically converts between the optional source value and an
  ///   unwrapped (possibly
  ///   <c>null</c>) value on the view side.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  static member oneWayOpt
      (get: 'model -> 'a voption)
      : string -> Binding<'model, 'msg> =
    { Get = get }
    |> OneWayData.boxVOpt
    |> OneWayData
    |> createBinding


  /// <summary>
  ///   Creates a lazily evaluated one-way binding. <paramref name="map" />
  ///   will be called only when the output of <paramref name="get" /> changes,
  ///   as determined by <paramref name="equals" />. This may have better
  ///   performance than <see cref="oneWay" /> for expensive computations (but
  ///   may be less performant for non-expensive functions due to additional
  ///   overhead).
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="equals">
  ///   Indicates whether two intermediate values are equal. Good candidates are
  ///   <c>elmEq</c> and <c>refEq</c>.
  /// </param>
  /// <param name="map">Transforms the value into the final type.</param>
  static member oneWayLazy
      (get: 'model -> 'a,
       equals: 'a -> 'a -> bool,
       map: 'a -> 'b)
      : string -> Binding<'model, 'msg> =
    { Get = get
      Map = map
      Equals = equals }
    |> OneWayLazyData.box
    |> OneWayLazyData
    |> createBinding


  /// <summary>
  ///   Creates a lazily evaluated one-way binding to an optional value. The
  ///   binding automatically converts between the optional source value and an
  ///   unwrapped (possibly <c>null</c>) value on the view side. <paramref
  ///   name="map" /> will be called only when the output of <paramref
  ///   name="get" /> changes, as determined by <paramref name="equals" />.
  ///
  ///   This may have better performance than a non-lazy binding for expensive
  ///   computations (but may be less performant for non-expensive functions due
  ///   to additional overhead).
  /// </summary>
  /// <param name="get">Gets the intermediate value from the model.</param>
  /// <param name="equals">
  ///   Indicates whether two intermediate values are equal. Good candidates are
  ///   <c>elmEq</c> and <c>refEq</c>.
  /// </param>
  /// <param name="map">Transforms the intermediate value into the final
  /// type.</param>
  static member oneWayOptLazy
      (get: 'model -> 'a,
       equals: 'a -> 'a -> bool,
       map: 'a -> 'b option)
      : string -> Binding<'model, 'msg> =
    { Get = get
      Map = map
      Equals = equals }
    |> OneWayLazyData.boxOpt
    |> OneWayLazyData
    |> createBinding


  /// <summary>
  ///   Creates a lazily evaluated one-way binding to an optional value. The
  ///   binding automatically converts between the optional source value and an
  ///   unwrapped (possibly <c>null</c>) value on the view side. <paramref
  ///   name="map" /> will be called only when the output of <paramref
  ///   name="get" /> changes, as determined by <paramref name="equals" />.
  ///
  ///   This may have better performance than a non-lazy binding for expensive
  ///   computations (but may be less performant for non-expensive functions due
  ///   to additional overhead).
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="equals">
  ///   Indicates whether two intermediate values are equal. Good candidates are
  ///   <c>elmEq</c> and <c>refEq</c>.
  /// </param>
  /// <param name="map">Transforms the intermediate value into the final
  /// type.</param>
  static member oneWayOptLazy
      (get: 'model -> 'a,
       equals: 'a -> 'a -> bool,
       map: 'a -> 'b voption)
      : string -> Binding<'model, 'msg> =
    { Get = get
      Map = map
      Equals = equals }
    |> OneWayLazyData.boxVOpt
    |> OneWayLazyData
    |> createBinding


  /// <summary>
  ///   Creates a one-way binding to a sequence of items, each uniquely
  ///   identified by the value returned by <paramref name="getId"/>. The
  ///   binding will not be updated if the output of <paramref name="get"/>
  ///   does not change, as determined by <paramref name="equals"/>.
  ///   The binding is backed by a persistent <c>ObservableCollection</c>, so
  ///   only changed items (as determined by <paramref name="itemEquals"/>)
  ///   will be replaced. If the items are complex and you want them updated
  ///   instead of replaced, consider using <see cref="subModelSeq"/>.
  /// </summary>
  /// <param name="get">Gets the intermediate value from the model.</param>
  /// <param name="equals">
  ///   Indicates whether two intermediate values are equal. Good candidates are
  ///   <c>elmEq</c> and <c>refEq</c>.
  /// </param>
  /// <param name="map">Transforms the value into the final collection.</param>
  /// <param name="itemEquals">
  ///   Indicates whether two collection items are equal. Good candidates are
  ///   <c>elmEq</c>, <c>refEq</c>, or simply <c>(=)</c>.
  /// </param>
  /// <param name="getId">Gets a unique identifier for a collection
  /// item.</param>
  static member oneWaySeqLazy
      (get: 'model -> 'a,
       equals: 'a -> 'a -> bool,
       map: 'a -> #seq<'b>,
       itemEquals: 'b -> 'b -> bool,
       getId: 'b -> 'id)
      : string -> Binding<'model, 'msg> =
    { Get = get
      Map = fun a -> upcast map a
      Equals = equals
      GetId = getId
      ItemEquals = itemEquals }
    |> OneWaySeqLazyData.box
    |> OneWaySeqLazyData
    |> createBinding

    
  /// <summary>
  ///   Creates a one-way binding to a sequence of items, each uniquely
  ///   identified by the value returned by <paramref name="getId"/>. The
  ///   binding will not be updated if the output of <paramref name="get"/>
  ///   is referentially equal. This is the same as calling
  ///   <see cref="oneWaySeqLazy"/> with <c>equals = refEq</c> and
  ///   <c>map = id</c>. The binding is backed by a persistent
  ///   <c>ObservableCollection</c>, so only changed items (as determined by
  ///   <paramref name="itemEquals"/>) will be replaced. If the items are
  ///   complex and you want them updated instead of replaced, consider using
  ///   <see cref="subModelSeq"/>.
  /// </summary>
  /// <param name="get">Gets the collection from the model.</param>
  /// <param name="itemEquals">
  ///   Indicates whether two collection items are equal. Good candidates are
  ///   <c>elmEq</c>, <c>refEq</c>, or simply <c>(=)</c>.
  /// </param>
  /// <param name="getId">Gets a unique identifier for a collection
  /// item.</param>
  static member oneWaySeq
      (get: 'model -> #seq<'a>,
       itemEquals: 'a -> 'a -> bool,
       getId: 'a -> 'id)
      : string -> Binding<'model, 'msg> =
    Binding.oneWaySeqLazy(get, refEq, id, itemEquals, getId)


  /// <summary>Creates a two-way binding.</summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  static member twoWay
      (get: 'model -> 'a,
       set: 'a -> 'model -> 'msg)
      : string -> Binding<'model, 'msg> =
    { Get = get
      Set = set }
    |> TwoWayData.box
    |> TwoWayData
    |> createBinding


  /// <summary>
  ///   Creates a two-way binding to an optional value. The binding
  ///   automatically converts between the optional source value and an
  ///   unwrapped (possibly <c>null</c>) value on the view side.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  static member twoWayOpt
      (get: 'model -> 'a option,
       set: 'a option -> 'model -> 'msg)
      : string -> Binding<'model, 'msg> =
    { Get = get
      Set = set }
    |> TwoWayData.boxOpt
    |> TwoWayData
    |> createBinding


  /// <summary>
  ///   Creates a two-way binding to an optional value. The binding
  ///   automatically converts between the optional source value and an
  ///   unwrapped (possibly <c>null</c>) value on the view side.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  static member twoWayOpt
      (get: 'model -> 'a voption,
       set: 'a voption -> 'model -> 'msg)
      : string -> Binding<'model, 'msg> =
    { Get = get
      Set = set }
    |> TwoWayData.boxVOpt
    |> TwoWayData
    |> createBinding


  /// <summary>
  ///   Creates a two-way binding with validation using
  ///   <c>INotifyDataErrorInfo</c>.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="validate">
  ///   Returns the validation messages from the updated model.
  /// </param>
  static member twoWayValidate
      (get: 'model -> 'a,
       set: 'a -> 'model -> 'msg,
       validate: 'model -> string list)
      : string -> Binding<'model, 'msg> =
    { Get = get
      Set = set
      Validate = validate }
    |> TwoWayValidateData.box
    |> TwoWayValidateData
    |> createBinding


  /// <summary>
  ///   Creates a two-way binding with validation using
  ///   <c>INotifyDataErrorInfo</c>.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="validate">
  ///   Returns the validation message from the updated model.
  /// </param>
  static member twoWayValidate
      (get: 'model -> 'a,
       set: 'a -> 'model -> 'msg,
       validate: 'model -> string voption)
      : string -> Binding<'model, 'msg> =
    { Get = get
      Set = set
      Validate = validate >> ValueOption.toList }
    |> TwoWayValidateData.box
    |> TwoWayValidateData
    |> createBinding


  /// <summary>
  ///   Creates a two-way binding with validation using
  ///   <c>INotifyDataErrorInfo</c>.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="validate">
  ///   Returns the validation message from the updated model.
  /// </param>
  static member twoWayValidate
      (get: 'model -> 'a,
       set: 'a -> 'model -> 'msg,
       validate: 'model -> string option)
      : string -> Binding<'model, 'msg> =
    { Get = get
      Set = set
      Validate = validate >> Option.toList }
    |> TwoWayValidateData.box
    |> TwoWayValidateData
    |> createBinding


  /// <summary>
  ///   Creates a two-way binding with validation using
  ///   <c>INotifyDataErrorInfo</c>.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="validate">
  ///   Returns the validation message from the updated model.
  /// </param>
  static member twoWayValidate
      (get: 'model -> 'a,
       set: 'a -> 'model -> 'msg,
       validate: 'model -> Result<'ignored, string>)
      : string -> Binding<'model, 'msg> =
    { Get = get
      Set = set
      Validate = validate >> ValueOption.ofError >> ValueOption.toList }
    |> TwoWayValidateData.box
    |> TwoWayValidateData
    |> createBinding


  /// <summary>
  ///   Creates a two-way binding to an optional value with validation using
  ///   <c>INotifyDataErrorInfo</c>. The binding automatically converts between
  ///   the optional source value and an unwrapped (possibly <c>null</c>) value
  ///   on the view side.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="validate">
  ///   Returns the validation messages from the updated model.
  /// </param>
  static member twoWayOptValidate
      (get: 'model -> 'a voption,
       set: 'a voption -> 'model -> 'msg,
       validate: 'model -> string list)
      : string -> Binding<'model, 'msg> =
    { Get = get
      Set = set
      Validate = validate }
    |> TwoWayValidateData.boxVOpt
    |> TwoWayValidateData
    |> createBinding


  /// <summary>
  ///   Creates a two-way binding to an optional value with validation using
  ///   <c>INotifyDataErrorInfo</c>. The binding automatically converts between
  ///   the optional source value and an unwrapped (possibly <c>null</c>) value
  ///   on the view side.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="validate">
  ///   Returns the validation message from the updated model.
  /// </param>
  static member twoWayOptValidate
      (get: 'model -> 'a voption,
       set: 'a voption -> 'model -> 'msg,
       validate: 'model -> string voption)
      : string -> Binding<'model, 'msg> =
    { Get = get
      Set = set
      Validate = validate >> ValueOption.toList }
    |> TwoWayValidateData.boxVOpt
    |> TwoWayValidateData
    |> createBinding


  /// <summary>
  ///   Creates a two-way binding to an optional value with validation using
  ///   <c>INotifyDataErrorInfo</c>. The binding automatically converts between
  ///   the optional source value and an unwrapped (possibly <c>null</c>) value
  ///   on the view side.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="validate">
  ///   Returns the validation message from the updated model.
  /// </param>
  static member twoWayOptValidate
      (get: 'model -> 'a voption,
       set: 'a voption -> 'model -> 'msg,
       validate: 'model -> string option)
      : string -> Binding<'model, 'msg> =
    { Get = get
      Set = set
      Validate = validate >> Option.toList }
    |> TwoWayValidateData.boxVOpt
    |> TwoWayValidateData
    |> createBinding


  /// <summary>
  ///   Creates a two-way binding to an optional value with validation using
  ///   <c>INotifyDataErrorInfo</c>. The binding automatically converts between
  ///   the optional source value and an unwrapped (possibly <c>null</c>) value
  ///   on the view side.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="validate">
  ///   Returns the validation message from the updated model.
  /// </param>
  static member twoWayOptValidate
      (get: 'model -> 'a voption,
       set: 'a voption -> 'model -> 'msg,
       validate: 'model -> Result<'ignored, string>)
      : string -> Binding<'model, 'msg> =
    { Get = get
      Set = set
      Validate = validate >> ValueOption.ofError >> ValueOption.toList }
    |> TwoWayValidateData.boxVOpt
    |> TwoWayValidateData
    |> createBinding


  /// <summary>
  ///   Creates a two-way binding to an optional value with validation using
  ///   <c>INotifyDataErrorInfo</c>. The binding automatically converts between
  ///   the optional source value and an unwrapped (possibly <c>null</c>) value
  ///   on the view side.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="validate">
  ///   Returns the validation messages from the updated model.
  /// </param>
  static member twoWayOptValidate
      (get: 'model -> 'a option,
       set: 'a option -> 'model -> 'msg,
       validate: 'model -> string list)
      : string -> Binding<'model, 'msg> =
    { Get = get
      Set = set
      Validate = validate }
    |> TwoWayValidateData.boxOpt
    |> TwoWayValidateData
    |> createBinding


  /// <summary>
  ///   Creates a two-way binding to an optional value with validation using
  ///   <c>INotifyDataErrorInfo</c>. The binding automatically converts between
  ///   the optional source value and an unwrapped (possibly <c>null</c>) value
  ///   on the view side.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="validate">
  ///   Returns the validation message from the updated model.
  /// </param>
  static member twoWayOptValidate
      (get: 'model -> 'a option,
       set: 'a option -> 'model -> 'msg,
       validate: 'model -> string voption)
      : string -> Binding<'model, 'msg> =
    { Get = get
      Set = set
      Validate = validate >> ValueOption.toList }
    |> TwoWayValidateData.boxOpt
    |> TwoWayValidateData
    |> createBinding


  /// <summary>
  ///   Creates a two-way binding to an optional value with validation using
  ///   <c>INotifyDataErrorInfo</c>. The binding automatically converts between
  ///   the optional source value and an unwrapped (possibly <c>null</c>) value
  ///   on the view side.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="validate">
  ///   Returns the validation message from the updated model.
  /// </param>
  static member twoWayOptValidate
      (get: 'model -> 'a option,
       set: 'a option -> 'model -> 'msg,
       validate: 'model -> string option)
      : string -> Binding<'model, 'msg> =
    { Get = get
      Set = set
      Validate = validate >> Option.toList }
    |> TwoWayValidateData.boxOpt
    |> TwoWayValidateData
    |> createBinding


  /// <summary>
  ///   Creates a two-way binding to an optional value with validation using
  ///   <c>INotifyDataErrorInfo</c>. The binding automatically converts between
  ///   the optional source value and an unwrapped (possibly <c>null</c>) value
  ///   on the view side.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="validate">
  ///   Returns the validation message from the updated model.
  /// </param>
  static member twoWayOptValidate
      (get: 'model -> 'a option,
       set: 'a option -> 'model -> 'msg,
       validate: 'model -> Result<'ignored, string>)
      : string -> Binding<'model, 'msg> =
    { Get = get
      Set = set
      Validate = validate >> ValueOption.ofError >> ValueOption.toList }
    |> TwoWayValidateData.boxOpt
    |> TwoWayValidateData
    |> createBinding


  /// <summary>
  ///   Creates a <c>Command</c> binding that depends only on the model (not the
  ///   <c>CommandParameter</c>) and can always execute.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  static member cmd
      (exec: 'model -> 'msg)
      : string -> Binding<'model, 'msg> =
    { Exec = exec >> ValueSome
      CanExec = fun _ -> true }
    |> CmdData
    |> createBinding


  /// <summary>
  ///   Creates a conditional <c>Command</c> binding that depends only on the
  ///   model (not the <c>CommandParameter</c>) and can execute if <paramref
  ///   name="canExec" />
  ///   returns <c>true</c>.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  /// <param name="canExec">Indicates whether the command can execute.</param>
  static member cmdIf
      (exec: 'model -> 'msg,
       canExec: 'model -> bool)
      : string -> Binding<'model, 'msg> =
    { Exec = exec >> ValueSome
      CanExec = canExec }
    |> CmdData
    |> createBinding


  /// <summary>
  ///   Creates a conditional <c>Command</c> binding that depends only on the
  ///   model (not the <c>CommandParameter</c>) and can execute if <paramref
  ///   name="exec" />
  ///   returns <c>ValueSome</c>.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  static member cmdIf
      (exec: 'model -> 'msg voption)
      : string -> Binding<'model, 'msg> =
    { Exec = exec
      CanExec = exec >> ValueOption.isSome }
    |> CmdData
    |> createBinding


  /// <summary>
  ///   Creates a conditional <c>Command</c> binding that depends only on the
  ///   model (not the <c>CommandParameter</c>) and can execute if <paramref
  ///   name="exec" />
  ///   returns <c>Some</c>.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  static member cmdIf
      (exec: 'model -> 'msg option)
      : string -> Binding<'model, 'msg> =
    { Exec = exec >> ValueOption.ofOption
      CanExec = exec >> Option.isSome }
    |> CmdData
    |> createBinding


  /// <summary>
  ///   Creates a conditional <c>Command</c> binding that depends only on the
  ///   model (not the <c>CommandParameter</c>) and can execute if <paramref
  ///   name="exec" />
  ///   returns <c>Ok</c>.
  ///
  ///   This overload allows more easily re-using the same validation functions
  ///   for inputs and commands.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  static member cmdIf
      (exec: 'model -> Result<'msg, 'ignored>)
      : string -> Binding<'model, 'msg> =
    { Exec = exec >> ValueOption.ofOk
      CanExec = exec >> Result.isOk }
    |> CmdData
    |> createBinding


  /// <summary>
  ///   Creates a <c>Command</c> binding that depends on the
  ///   <c>CommandParameter</c>
  ///   and can always execute.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  static member cmdParam
      (exec: obj -> 'model -> 'msg)
      : string -> Binding<'model, 'msg> =
    { Exec = fun p model -> exec p model |> ValueSome
      CanExec = fun _ _ -> true
      AutoRequery = false }
    |> CmdParamData
    |> createBinding


  /// <summary>
  ///   Creates a <c>Command</c> binding that depends on the
  ///   <c>CommandParameter</c>
  ///   and can execute if <paramref name="canExec" /> returns <c>true</c>.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  /// <param name="canExec">Indicates whether the command can execute.</param>
  /// <param name="uiBoundCmdParam">
  ///   If <c>true</c>, <c>CanExecuteChanged</c> will trigger every time WPF's
  ///   <c>CommandManager</c>
  ///   detects UI changes that could potentially influence the command's
  ///   ability to execute. This will likely lead to many more triggers than
  ///   necessary, but is needed if you have bound the <c>CommandParameter</c>
  ///   to another UI property.
  /// </param>
  static member cmdParamIf
      (exec: obj -> 'model -> 'msg,
       canExec: obj -> 'model -> bool,
       ?uiBoundCmdParam: bool)
      : string -> Binding<'model, 'msg> =
    { Exec = fun p m -> exec p m |> ValueSome
      CanExec = canExec
      AutoRequery = defaultArg uiBoundCmdParam false }
    |> CmdParamData
    |> createBinding


  /// <summary>
  ///   Creates a conditional <c>Command</c> binding that depends on the
  ///   <c>CommandParameter</c>
  ///   and can execute if <paramref name="exec" /> returns <c>ValueSome</c>.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  /// <param name="uiBoundCmdParam">
  ///   If <c>true</c>, <c>CanExecuteChanged</c> will trigger every time WPF's
  ///   <c>CommandManager</c>
  ///   detects UI changes that could potentially influence the command's
  ///   ability to execute. This will likely lead to many more triggers than
  ///   necessary, but is needed if you have bound the <c>CommandParameter</c>
  ///   to another UI property.
  /// </param>
  static member cmdParamIf
      (exec: obj -> 'model -> 'msg voption,
       ?uiBoundCmdParam: bool)
      : string -> Binding<'model, 'msg> =
    { Exec = exec
      CanExec = fun p m -> exec p m |> ValueOption.isSome
      AutoRequery = defaultArg uiBoundCmdParam false }
    |> CmdParamData
    |> createBinding


  /// <summary>
  ///   Creates a conditional <c>Command</c> binding that depends on the
  ///   <c>CommandParameter</c>
  ///   and can execute if <paramref name="exec" /> returns <c>Some</c>.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  /// <param name="uiBoundCmdParam">
  ///   If <c>true</c>, <c>CanExecuteChanged</c> will trigger every time WPF's
  ///   <c>CommandManager</c>
  ///   detects UI changes that could potentially influence the command's
  ///   ability to execute. This will likely lead to many more triggers than
  ///   necessary, but is needed if you have bound the <c>CommandParameter</c>
  ///   to another UI property.
  /// </param>
  static member cmdParamIf
      (exec: obj -> 'model -> 'msg option,
       ?uiBoundCmdParam: bool)
      : string -> Binding<'model, 'msg> =
    { Exec = fun p m -> exec p m |> ValueOption.ofOption
      CanExec = fun p m -> exec p m |> Option.isSome
      AutoRequery = defaultArg uiBoundCmdParam false }
    |> CmdParamData
    |> createBinding


  /// <summary>
  ///   Creates a conditional <c>Command</c> binding that depends on the
  ///   <c>CommandParameter</c>
  ///   and can execute if <paramref name="exec" /> returns <c>Ok</c>.
  ///
  ///   This overload allows more easily re-using the same validation functions
  ///   for inputs and commands.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  /// <param name="uiBoundCmdParam">
  ///   If <c>true</c>, <c>CanExecuteChanged</c> will trigger every time WPF's
  ///   <c>CommandManager</c>
  ///   detects UI changes that could potentially influence the command's
  ///   ability to execute. This will likely lead to many more triggers than
  ///   necessary, but is needed if you have bound the <c>CommandParameter</c>
  ///   to another UI property.
  /// </param>
  static member cmdParamIf
      (exec: obj -> 'model -> Result<'msg, 'ignored>,
       ?uiBoundCmdParam: bool)
      : string -> Binding<'model, 'msg> =
    { Exec = fun p m -> exec p m |> ValueOption.ofOk
      CanExec = fun p m -> exec p m |> Result.isOk
      AutoRequery = defaultArg uiBoundCmdParam false }
    |> CmdParamData
    |> createBinding


  /// <summary>
  ///   Creates a binding to a sub-model/component that has its own bindings and
  ///   message type. You typically bind this to the <c>DataContext</c> of a
  ///   <c>UserControl</c> or similar.
  /// </summary>
  /// <param name="getSubModel">Gets the sub-model from the model.</param>
  /// <param name="toBindingModel">
  ///   Converts the models to the model used by the bindings.
  /// </param>
  /// <param name="toMsg">
  ///   Converts the messages used in the bindings to parent model messages
  ///   (e.g. a parent message union case that wraps the child message type).
  /// </param>
  /// <param name="bindings">Returns the bindings for the sub-model.</param>
  static member subModel
      (getSubModel: 'model -> 'subModel,
       toBindingModel: 'model * 'subModel -> 'bindingModel,
       toMsg: 'bindingMsg -> 'msg,
       bindings: unit -> Binding<'bindingModel, 'bindingMsg> list)
      : string -> Binding<'model, 'msg> =
    { GetModel = fun m -> toBindingModel (m, getSubModel m) |> ValueSome
      GetBindings = bindings
      ToMsg = fun _ -> toMsg
      Sticky = false }
    |> SubModelData.box
    |> SubModelData
    |> createBinding


  /// <summary>
  ///   Creates a binding to a sub-model/component that has its own bindings and
  ///   message type. You typically bind this to the <c>DataContext</c> of a
  ///   <c>UserControl</c> or similar.
  /// </summary>
  /// <param name="getSubModel">Gets the sub-model from the model.</param>
  /// <param name="toMsg">
  ///   Converts the messages used in the bindings to parent model messages
  ///   (e.g. a parent message union case that wraps the child message type).
  /// </param>
  /// <param name="bindings">Returns the bindings for the sub-model.</param>
  static member subModel
      (getSubModel: 'model -> 'subModel,
       toMsg: 'subMsg -> 'msg,
       bindings: unit -> Binding<'model * 'subModel, 'subMsg> list)
      : string -> Binding<'model, 'msg> =
    { GetModel = fun m -> (m, getSubModel m) |> ValueSome
      GetBindings = bindings
      ToMsg = fun _ -> toMsg
      Sticky = false }
    |> SubModelData.box
    |> SubModelData
    |> createBinding


  /// <summary>
  ///   Creates a binding to a sub-model/component that has its own bindings.
  ///   You typically bind this to the <c>DataContext</c> of a
  ///   <c>UserControl</c> or similar.
  /// </summary>
  /// <param name="getSubModel">Gets the sub-model from the model.</param>
  /// <param name="bindings">Returns the bindings for the sub-model.</param>
  static member subModel
      (getSubModel: 'model -> 'subModel,
       bindings: unit -> Binding<'model * 'subModel, 'msg> list)
      : string -> Binding<'model, 'msg> =
    { GetModel = fun m -> (m, getSubModel m) |> ValueSome
      GetBindings = bindings
      ToMsg = fun _ -> id
      Sticky = false }
    |> SubModelData.box
    |> SubModelData
    |> createBinding


  /// <summary>
  ///   Creates a binding to a sub-model/component that has its own bindings and
  ///   message type, and may not exist. If it does not exist, bindings to this
  ///   model will return <c>null</c> unless <paramref name="sticky" /> is
  ///   <c>true</c>, in which case the last non-<c>null</c> model will be
  ///   returned. You typically bind this to the <c>DataContext</c> of a
  ///   <c>UserControl</c> or similar.
  ///
  ///   The 'sticky' part is useful if you want to e.g. animate away a
  ///   <c>UserControl</c> when the model is missing, but don't want the data
  ///   used by that control to be cleared once the animation starts. (The
  ///   animation must be triggered using another binding since this will never
  ///   return <c>null</c>.)
  /// </summary>
  /// <param name="getSubModel">Gets the sub-model from the model.</param>
  /// <param name="toBindingModel">
  ///   Converts the models to the model used by the bindings.
  /// </param>
  /// <param name="toMsg">
  ///   Converts the messages used in the bindings to parent model messages
  ///   (e.g. a parent message union case that wraps the child message type).
  /// </param>
  /// <param name="bindings">Returns the bindings for the sub-model.</param>
  /// <param name="sticky">
  ///   If <c>true</c>, when the model is missing, the last non-<c>null</c>
  ///   model will be returned instead of <c>null</c>.
  /// </param>
  static member subModelOpt
      (getSubModel: 'model -> 'subModel voption,
       toBindingModel: 'model * 'subModel -> 'bindingModel,
       toMsg: 'bindingMsg -> 'msg,
       bindings: unit -> Binding<'bindingModel, 'bindingMsg> list,
       ?sticky: bool)
      : string -> Binding<'model, 'msg> =
    { GetModel = fun m ->
        getSubModel m
        |> ValueOption.map (fun sub -> toBindingModel (m, sub))
      GetBindings = bindings
      ToMsg = fun _ -> toMsg
      Sticky = defaultArg sticky false }
    |> SubModelData.box
    |> SubModelData
    |> createBinding


  /// <summary>
  ///   Creates a binding to a sub-model/component that has its own bindings and
  ///   message type, and may not exist. If it does not exist, bindings to this
  ///   model will return <c>null</c> unless <paramref name="sticky" /> is
  ///   <c>true</c>, in which case the last non-<c>null</c> model will be
  ///   returned. You typically bind this to the <c>DataContext</c> of a
  ///   <c>UserControl</c> or similar.
  ///
  ///   The 'sticky' part is useful if you want to e.g. animate away a
  ///   <c>UserControl</c> when the model is missing, but don't want the data
  ///   used by that control to be cleared once the animation starts. (The
  ///   animation must be triggered using another binding since this will never
  ///   return <c>null</c>.)
  /// </summary>
  /// <param name="getSubModel">Gets the sub-model from the model.</param>
  /// <param name="toBindingModel">
  ///   Converts the models to the model used by the bindings.
  /// </param>
  /// <param name="toMsg">
  ///   Converts the messages used in the bindings to parent model messages
  ///   (e.g. a parent message union case that wraps the child message type).
  /// </param>
  /// <param name="bindings">Returns the bindings for the sub-model.</param>
  /// <param name="sticky">
  ///   If <c>true</c>, when the model is missing, the last non-<c>null</c>
  ///   model will be returned instead of <c>null</c>.
  /// </param>
  static member subModelOpt
      (getSubModel: 'model -> 'subModel option,
       toBindingModel: 'model * 'subModel -> 'bindingModel,
       toMsg: 'bindingMsg -> 'msg,
       bindings: unit -> Binding<'bindingModel, 'bindingMsg> list,
       ?sticky: bool)
      : string -> Binding<'model, 'msg> =
    { GetModel = fun m ->
        getSubModel m
        |> ValueOption.ofOption
        |> ValueOption.map (fun sub -> toBindingModel (m, sub))
      GetBindings = bindings
      ToMsg = fun _ -> toMsg
      Sticky = defaultArg sticky false }
    |> SubModelData.box
    |> SubModelData
    |> createBinding


  /// <summary>
  ///   Creates a binding to a sub-model/component that has its own bindings and
  ///   message type, and may not exist. If it does not exist, bindings to this
  ///   model will return <c>null</c> unless <paramref name="sticky" /> is
  ///   <c>true</c>, in which case the last non-<c>null</c> model will be
  ///   returned. You typically bind this to the <c>DataContext</c> of a
  ///   <c>UserControl</c> or similar.
  ///
  ///   The 'sticky' part is useful if you want to e.g. animate away a
  ///   <c>UserControl</c> when the model is missing, but don't want the data
  ///   used by that control to be cleared once the animation starts. (The
  ///   animation must be triggered using another binding since this will never
  ///   return <c>null</c>.)
  /// </summary>
  /// <param name="getSubModel">Gets the sub-model from the model.</param>
  /// <param name="toMsg">
  ///   Converts the messages used in the bindings to parent model messages
  ///   (e.g. a parent message union case that wraps the child message type).
  /// </param>
  /// <param name="bindings">Returns the bindings for the sub-model.</param>
  /// <param name="sticky">
  ///   If <c>true</c>, when the model is missing, the last non-<c>null</c>
  ///   model will be returned instead of <c>null</c>.
  /// </param>
  static member subModelOpt
      (getSubModel: 'model -> 'subModel voption,
       toMsg: 'subMsg -> 'msg,
       bindings: unit -> Binding<'model * 'subModel, 'subMsg> list,
       ?sticky: bool)
      : string -> Binding<'model, 'msg> =
    { GetModel = fun m ->
        getSubModel m |> ValueOption.map (fun sub -> (m, sub))
      GetBindings = bindings
      ToMsg = fun _ -> toMsg
      Sticky = defaultArg sticky false }
    |> SubModelData.box
    |> SubModelData
    |> createBinding


  /// <summary>
  ///   Creates a binding to a sub-model/component that has its own bindings and
  ///   message type, and may not exist. If it does not exist, bindings to this
  ///   model will return <c>null</c> unless <paramref name="sticky" /> is
  ///   <c>true</c>, in which case the last non-<c>null</c> model will be
  ///   returned. You typically bind this to the <c>DataContext</c> of a
  ///   <c>UserControl</c> or similar.
  ///
  ///   The 'sticky' part is useful if you want to e.g. animate away a
  ///   <c>UserControl</c> when the model is missing, but don't want the data
  ///   used by that control to be cleared once the animation starts. (The
  ///   animation must be triggered using another binding since this will never
  ///   return <c>null</c>.)
  /// </summary>
  /// <param name="getSubModel">Gets the sub-model from the model.</param>
  /// <param name="toMsg">
  ///   Converts the messages used in the bindings to parent model messages
  ///   (e.g. a parent message union case that wraps the child message type).
  /// </param>
  /// <param name="bindings">Returns the bindings for the sub-model.</param>
  /// <param name="sticky">
  ///   If <c>true</c>, when the model is missing, the last non-<c>null</c>
  ///   model will be returned instead of <c>null</c>.
  /// </param>
  static member subModelOpt
      (getSubModel: 'model -> 'subModel option,
       toMsg: 'subMsg -> 'msg,
       bindings: unit -> Binding<'model * 'subModel, 'subMsg> list,
       ?sticky: bool)
      : string -> Binding<'model, 'msg> =
    { GetModel = fun m ->
        getSubModel m
        |> ValueOption.ofOption
        |> ValueOption.map (fun sub -> (m, sub))
      GetBindings = bindings
      ToMsg = fun _ -> toMsg
      Sticky = defaultArg sticky false }
    |> SubModelData.box
    |> SubModelData
    |> createBinding


  /// <summary>
  ///   Creates a binding to a sub-model/component that has its own bindings,
  ///   and may not exist. If it does not exist, bindings to this model will
  ///   return <c>null</c> unless <paramref name="sticky" /> is <c>true</c>, in
  ///   which case the last non-<c>null</c> model will be returned. You
  ///   typically bind this to the <c>DataContext</c> of a <c>UserControl</c> or
  ///   similar.
  ///
  ///   The 'sticky' part is useful if you want to e.g. animate away a
  ///   <c>UserControl</c> when the model is missing, but don't want the data
  ///   used by that control to be cleared once the animation starts. (The
  ///   animation must be triggered using another binding since this will never
  ///   return <c>null</c>.)
  /// </summary>
  /// <param name="getSubModel">Gets the sub-model from the model.</param>
  /// <param name="bindings">Returns the bindings for the sub-model.</param>
  /// <param name="sticky">
  ///   If <c>true</c>, when the model is missing, the last non-<c>null</c>
  ///   model will be returned instead of <c>null</c>.
  /// </param>
  static member subModelOpt
      (getSubModel: 'model -> 'subModel voption,
       bindings: unit -> Binding<'model * 'subModel, 'msg> list,
       ?sticky: bool)
      : string -> Binding<'model, 'msg> =
    { GetModel = fun m ->
        getSubModel m
        |> ValueOption.map (fun sub -> (m, sub))
      GetBindings = bindings
      ToMsg = fun _ -> id
      Sticky = defaultArg sticky false }
    |> SubModelData.box
    |> SubModelData
    |> createBinding


  /// <summary>
  ///   Creates a binding to a sub-model/component that has its own bindings,
  ///   and may not exist. If it does not exist, bindings to this model will
  ///   return <c>null</c> unless <paramref name="sticky" /> is <c>true</c>, in
  ///   which case the last non-<c>null</c> model will be returned. You
  ///   typically bind this to the <c>DataContext</c> of a <c>UserControl</c> or
  ///   similar.
  ///
  ///   The 'sticky' part is useful if you want to e.g. animate away a
  ///   <c>UserControl</c> when the model is missing, but don't want the data
  ///   used by that control to be cleared once the animation starts. (The
  ///   animation must be triggered using another binding since this will never
  ///   return <c>null</c>.)
  /// </summary>
  /// <param name="getSubModel">Gets the sub-model from the model.</param>
  /// <param name="bindings">Returns the bindings for the sub-model.</param>
  /// <param name="sticky">
  ///   If <c>true</c>, when the model is missing, the last non-<c>null</c>
  ///   model will be returned instead of <c>null</c>.
  /// </param>
  static member subModelOpt
      (getSubModel: 'model -> 'subModel option,
       bindings: unit -> Binding<'model * 'subModel, 'msg> list,
       ?sticky: bool)
      : string -> Binding<'model, 'msg> =
    { GetModel = fun m ->
        getSubModel m
        |> ValueOption.ofOption
        |> ValueOption.map (fun sub -> (m, sub))
      GetBindings = bindings
      ToMsg = fun _ -> id
      Sticky = defaultArg sticky false }
    |> SubModelData.box
    |> SubModelData
    |> createBinding

  /// <summary>
  ///   Like <see cref="subModelOpt" />, but uses the <c>WindowState</c> wrapper
  ///   to show/hide/close a new window that will have the specified bindings as
  ///   its <c>DataContext</c>.
  ///
  ///   You do not need to set the <c>DataContext</c> yourself (neither in code
  ///   nor XAML).
  ///
  ///   The window can only be closed/hidden by changing the return value of
  ///   <paramref name="getState" />, and can not be directly closed by the
  ///   user. External close attempts (the Close/X button, Alt+F4, or System
  ///   Menu -> Close) will cause the message specified by
  ///   <paramref name="onCloseRequested" /> to be dispatched. You should supply
  ///   <paramref name="onCloseRequested" /> and react to this in a manner that
  ///   will not confuse a user trying to close the window (e.g. by closing it,
  ///   or displaying relevant feedback to the user.)
  ///
  ///   If you don't nead a sub-model, you can use
  ///   <c>WindowState&lt;unit&gt;</c> to just control the Window visibility,
  ///   and pass <c>fst</c> to <paramref name="toBindingModel" />.
  /// </summary>
  /// <param name="getState">Gets the window state and a sub-model.</param>
  /// <param name="toBindingModel">
  ///   Converts the models to the model used by the bindings.
  /// </param>
  /// <param name="toMsg">
  ///   Converts the messages used in the bindings to parent model messages
  ///   (e.g. a parent message union case that wraps the child message type).
  /// </param>
  /// <param name="bindings">Returns the bindings for the sub-model.</param>
  /// <param name="getWindow">
  ///   The function used to get and configure the window.
  /// </param>
  /// <param name="onCloseRequested">
  ///   The message to be dispatched on external close attempts (the Close/X
  ///   button, Alt+F4, or System Menu -> Close).
  /// </param>
  /// <param name="isModal">
  ///   Specifies whether the window will be shown modally (using
  ///   window.ShowDialog, blocking the rest of the app) or non-modally (using
  ///   window.Show).
  /// </param>
  static member subModelWin
      (getState: 'model -> WindowState<'subModel>,
       toBindingModel: 'model * 'subModel -> 'bindingModel,
       toMsg: 'bindingMsg -> 'msg,
       bindings: unit -> Binding<'bindingModel, 'bindingMsg> list,
       getWindow: 'model -> Dispatch<'msg> -> #Window,
       ?onCloseRequested: 'msg,
       ?isModal: bool)
      : string -> Binding<'model, 'msg> =
    { GetState = fun m ->
        getState m |>
        WindowState.map (fun sub -> toBindingModel (m, sub))
      GetBindings = bindings
      ToMsg = fun _ -> toMsg
      GetWindow = fun m d -> upcast getWindow m d
      IsModal = defaultArg isModal false
      OnCloseRequested = fun _ -> defaultArg (onCloseRequested |> Option.map ValueSome) ValueNone }
    |> SubModelWinData.box
    |> SubModelWinData
    |> createBinding


  /// <summary>
  ///   Like <see cref="subModelOpt" />, but uses the <c>WindowState</c> wrapper
  ///   to show/hide/close a new window that will have the specified bindings as
  ///   its <c>DataContext</c>.
  ///
  ///   You do not need to set the <c>DataContext</c> yourself (neither in code
  ///   nor XAML).
  ///
  ///   The window can only be closed/hidden by changing the return value of
  ///   <paramref name="getState" />, and can not be directly closed by the
  ///   user. External close attempts (the Close/X button, Alt+F4, or System
  ///   Menu -> Close) will cause the message specified by
  ///   <paramref name="onCloseRequested" /> to be dispatched. You should supply
  ///   <paramref name="onCloseRequested" /> and react to this in a manner that
  ///   will not confuse a user trying to close the window (e.g. by closing it,
  ///   or displaying relevant feedback to the user.)
  ///
  ///   If you don't nead a sub-model, you can use
  ///   <c>WindowState&lt;unit&gt;</c> to just control the Window visibility,
  ///   and pass <c>fst</c> to <paramref name="toBindingModel" />.
  /// </summary>
  /// <param name="getState">Gets the window state and a sub-model.</param>
  /// <param name="toBindingModel">
  ///   Converts the models to the model used by the bindings.
  /// </param>
  /// <param name="toMsg">
  ///   Converts the messages used in the bindings to parent model messages
  ///   (e.g. a parent message union case that wraps the child message type).
  /// </param>
  /// <param name="bindings">Returns the bindings for the sub-model.</param>
  /// <param name="getWindow">
  ///   The function used to get and configure the window.
  /// </param>
  /// <param name="onCloseRequested">
  ///   The message to be dispatched on external close attempts (the Close/X
  ///   button, Alt+F4, or System Menu -> Close).
  /// </param>
  /// <param name="isModal">
  ///   Specifies whether the window will be shown modally (using
  ///   window.ShowDialog, blocking the rest of the app) or non-modally (using
  ///   window.Show).
  /// </param>
  static member subModelWin
      (getState: 'model -> WindowState<'subModel>,
       toBindingModel: 'model * 'subModel -> 'bindingModel,
       toMsg: 'bindingMsg -> 'msg,
       bindings: unit -> Binding<'bindingModel, 'bindingMsg> list,
       getWindow: unit -> #Window,
       ?onCloseRequested: 'msg,
       ?isModal: bool)
      : string -> Binding<'model, 'msg> =
    Binding.subModelWin(
      getState,
      toBindingModel,
      toMsg,
      bindings,
      (fun _ _ -> getWindow ()),
      ?onCloseRequested = onCloseRequested,
      ?isModal = isModal
    )


  /// <summary>
  ///   Like <see cref="subModelOpt" />, but uses the <c>WindowState</c> wrapper
  ///   to show/hide/close a new window that will have the specified bindings as
  ///   its <c>DataContext</c>.
  ///
  ///   You do not need to set the <c>DataContext</c> yourself (neither in code
  ///   nor XAML).
  ///
  ///   The window can only be closed/hidden by changing the return value of
  ///   <paramref name="getState" />, and can not be directly closed by the
  ///   user. External close attempts (the Close/X button, Alt+F4, or System
  ///   Menu -> Close) will cause the message specified by
  ///   <paramref name="onCloseRequested" /> to be dispatched. You should supply
  ///   <paramref name="onCloseRequested" /> and react to this in a manner that
  ///   will not confuse a user trying to close the window (e.g. by closing it,
  ///   or displaying relevant feedback to the user.)
  /// </summary>
  /// <param name="getState">Gets the window state and a sub-model.</param>
  /// <param name="toMsg">
  ///   Converts the messages used in the bindings to parent model messages
  ///   (e.g. a parent message union case that wraps the child message type).
  /// </param>
  /// <param name="bindings">Returns the bindings for the sub-model.</param>
  /// <param name="getWindow">
  ///   The function used to get and configure the window.
  /// </param>
  /// <param name="onCloseRequested">
  ///   The message to be dispatched on external close attempts (the Close/X
  ///   button, Alt+F4, or System Menu -> Close).
  /// </param>
  /// <param name="isModal">
  ///   Specifies whether the window will be shown modally (using
  ///   window.ShowDialog, blocking the rest of the app) or non-modally (using
  ///   window.Show).
  /// </param>
  static member subModelWin
      (getState: 'model -> WindowState<'subModel>,
       toMsg: 'subMsg -> 'msg,
       bindings: unit -> Binding<'model * 'subModel, 'subMsg> list,
       getWindow: 'model -> Dispatch<'msg> -> #Window,
       ?onCloseRequested: 'msg,
       ?isModal: bool)
      : string -> Binding<'model, 'msg> =
    { GetState = fun m ->
        getState m
        |> WindowState.map (fun sub -> (m, sub))
      GetBindings = bindings
      ToMsg = fun _ -> toMsg
      GetWindow = fun m d -> upcast getWindow m d
      IsModal = defaultArg isModal false
      OnCloseRequested = fun _ -> defaultArg (onCloseRequested |> Option.map ValueSome) ValueNone }
    |> SubModelWinData.box
    |> SubModelWinData
    |> createBinding


  /// <summary>
  ///   Like <see cref="subModelOpt" />, but uses the <c>WindowState</c> wrapper
  ///   to show/hide/close a new window that will have the specified bindings as
  ///   its <c>DataContext</c>.
  ///
  ///   You do not need to set the <c>DataContext</c> yourself (neither in code
  ///   nor XAML).
  ///
  ///   The window can only be closed/hidden by changing the return value of
  ///   <paramref name="getState" />, and can not be directly closed by the
  ///   user. External close attempts (the Close/X button, Alt+F4, or System
  ///   Menu -> Close) will cause the message specified by
  ///   <paramref name="onCloseRequested" /> to be dispatched. You should supply
  ///   <paramref name="onCloseRequested" /> and react to this in a manner that
  ///   will not confuse a user trying to close the window (e.g. by closing it,
  ///   or displaying relevant feedback to the user.)
  /// </summary>
  /// <param name="getState">Gets the window state and a sub-model.</param>
  /// <param name="toMsg">
  ///   Converts the messages used in the bindings to parent model messages
  ///   (e.g. a parent message union case that wraps the child message type).
  /// </param>
  /// <param name="bindings">Returns the bindings for the sub-model.</param>
  /// <param name="getWindow">
  ///   The function used to get and configure the window.
  /// </param>
  /// <param name="onCloseRequested">
  ///   The message to be dispatched on external close attempts (the Close/X
  ///   button, Alt+F4, or System Menu -> Close).
  /// </param>
  /// <param name="isModal">
  ///   Specifies whether the window will be shown modally (using
  ///   window.ShowDialog, blocking the rest of the app) or non-modally (using
  ///   window.Show).
  /// </param>
  static member subModelWin
      (getState: 'model -> WindowState<'subModel>,
       toMsg: 'subMsg -> 'msg,
       bindings: unit -> Binding<'model * 'subModel, 'subMsg> list,
       getWindow: unit -> #Window,
       ?onCloseRequested: 'msg,
       ?isModal: bool)
      : string -> Binding<'model, 'msg> =
    Binding.subModelWin(
      getState,
      toMsg,
      bindings,
      (fun _ _ -> getWindow ()),
      ?onCloseRequested = onCloseRequested,
      ?isModal = isModal
    )


  /// <summary>
  ///   Like <see cref="subModelOpt" />, but uses the <c>WindowState</c> wrapper
  ///   to show/hide/close a new window that will have the specified bindings as
  ///   its <c>DataContext</c>.
  ///
  ///   You do not need to set the <c>DataContext</c> yourself (neither in code
  ///   nor XAML).
  ///
  ///   The window can only be closed/hidden by changing the return value of
  ///   <paramref name="getState" />, and can not be directly closed by the
  ///   user. External close attempts (the Close/X button, Alt+F4, or System
  ///   Menu -> Close) will cause the message specified by
  ///   <paramref name="onCloseRequested" /> to be dispatched. You should supply
  ///   <paramref name="onCloseRequested" /> and react to this in a manner that
  ///   will not confuse a user trying to close the window (e.g. by closing it,
  ///   or displaying relevant feedback to the user.)
  /// </summary>
  /// <param name="getState">Gets the window state and a sub-model.</param>
  /// <param name="bindings">Returns the bindings for the sub-model.</param>
  /// <param name="getWindow">
  ///   The function used to get and configure the window.
  /// </param>
  /// <param name="onCloseRequested">
  ///   The message to be dispatched on external close attempts (the Close/X
  ///   button, Alt+F4, or System Menu -> Close).
  /// </param>
  /// <param name="isModal">
  ///   Specifies whether the window will be shown modally (using
  ///   window.ShowDialog, blocking the rest of the app) or non-modally (using
  ///   window.Show).
  /// </param>
  static member subModelWin
      (getState: 'model -> WindowState<'subModel>,
       bindings: unit -> Binding<'model * 'subModel, 'msg> list,
       getWindow: 'model -> Dispatch<'msg> -> #Window,
       ?onCloseRequested: 'msg,
       ?isModal: bool)
      : string -> Binding<'model, 'msg> =
    { GetState = fun m ->
        getState m
        |> WindowState.map (fun sub -> (m, sub))
      GetBindings = bindings
      ToMsg = fun _ -> id
      GetWindow = fun m d -> upcast getWindow m d
      IsModal = defaultArg isModal false
      OnCloseRequested = fun _ -> defaultArg (onCloseRequested |> Option.map ValueSome) ValueNone }
    |> SubModelWinData.box
    |> SubModelWinData
    |> createBinding


  /// <summary>
  ///   Like <see cref="subModelOpt" />, but uses the <c>WindowState</c> wrapper
  ///   to show/hide/close a new window that will have the specified bindings as
  ///   its <c>DataContext</c>.
  ///
  ///   You do not need to set the <c>DataContext</c> yourself (neither in code
  ///   nor XAML).
  ///
  ///   The window can only be closed/hidden by changing the return value of
  ///   <paramref name="getState" />, and can not be directly closed by the
  ///   user. External close attempts (the Close/X button, Alt+F4, or System
  ///   Menu -> Close) will cause the message specified by
  ///   <paramref name="onCloseRequested" /> to be dispatched. You should supply
  ///   <paramref name="onCloseRequested" /> and react to this in a manner that
  ///   will not confuse a user trying to close the window (e.g. by closing it,
  ///   or displaying relevant feedback to the user.)
  /// </summary>
  /// <param name="getState">Gets the window state and a sub-model.</param>
  /// <param name="bindings">Returns the bindings for the sub-model.</param>
  /// <param name="getWindow">
  ///   The function used to get and configure the window.
  /// </param>
  /// <param name="onCloseRequested">
  ///   The message to be dispatched on external close attempts (the Close/X
  ///   button, Alt+F4, or System Menu -> Close).
  /// </param>
  /// <param name="isModal">
  ///   Specifies whether the window will be shown modally (using
  ///   window.ShowDialog, blocking the rest of the app) or non-modally (using
  ///   window.Show).
  /// </param>
  static member subModelWin
      (getState: 'model -> WindowState<'subModel>,
       bindings: unit -> Binding<'model * 'subModel, 'msg> list,
       getWindow: unit -> #Window,
       ?onCloseRequested: 'msg,
       ?isModal: bool)
      : string -> Binding<'model, 'msg> =
    Binding.subModelWin(
      getState,
      bindings,
      (fun _ _ -> getWindow ()),
      ?onCloseRequested = onCloseRequested,
      ?isModal = isModal
    )


  /// <summary>
  ///   Creates a binding to a sequence of sub-models, each uniquely identified
  ///   by the value returned by <paramref name="getId" />. The sub-models have
  ///   their own bindings and message type. You typically bind this to the
  ///   <c>ItemsSource</c> of an <c>ItemsControl</c>, <c>ListView</c>,
  ///   <c>TreeView</c>, etc.
  /// </summary>
  /// <param name="getSubModels">Gets the sub-models from the model.</param>
  /// <param name="toBindingModel">
  ///   Converts the models to the model used by the bindings.
  /// </param>
  /// <param name="getId">Gets a unique identifier for a sub-model.</param>
  /// <param name="toMsg">
  ///   Converts the sub-model ID and messages used in the bindings to parent
  ///   model messages (e.g. a parent message union case that wraps the
  ///   sub-model ID and message type).
  /// </param>
  /// <param name="bindings">Returns the bindings for the sub-model.</param>
  static member subModelSeq
      (getSubModels: 'model -> #seq<'subModel>,
       toBindingModel: 'model * 'subModel -> 'bindingModel,
       getId: 'bindingModel -> 'id,
       toMsg: 'id * 'bindingMsg -> 'msg,
       bindings: unit -> Binding<'bindingModel, 'bindingMsg> list)
      : string -> Binding<'model, 'msg> =
    { GetModels = fun m ->
        getSubModels m
        |> Seq.map (fun sub -> toBindingModel (m, sub))
      GetId = getId
      GetBindings = bindings
      ToMsg = fun _ -> toMsg }
    |> SubModelSeqData.box
    |> SubModelSeqData
    |> createBinding


  /// <summary>
  ///   Creates a binding to a sequence of sub-models, each uniquely identified
  ///   by the value returned by <paramref name="getId" />. The sub-models have
  ///   their own bindings and message type. You typically bind this to the
  ///   <c>ItemsSource</c> of an <c>ItemsControl</c>, <c>ListView</c>,
  ///   <c>TreeView</c>, etc.
  /// </summary>
  /// <param name="getSubModels">Gets the sub-models from the model.</param>
  /// <param name="getId">Gets a unique identifier for a sub-model.</param>
  /// <param name="toMsg">
  ///   Converts the sub-model ID and messages used in the bindings to parent
  ///   model messages (e.g. a parent message union case that wraps the
  ///   sub-model ID and message type).
  /// </param>
  /// <param name="bindings">Returns the bindings for the sub-model.</param>
  static member subModelSeq
      (getSubModels: 'model -> #seq<'subModel>,
       getId: 'subModel -> 'id,
       toMsg: 'id * 'subMsg -> 'msg,
       bindings: unit -> Binding<'model * 'subModel, 'subMsg> list)
      : string -> Binding<'model, 'msg> =
    { GetModels = fun m ->
        getSubModels m
        |> Seq.map (fun sub -> (m, sub))
      GetId = snd >> getId
      GetBindings = bindings
      ToMsg = fun _ -> toMsg }
    |> SubModelSeqData.box
    |> SubModelSeqData
    |> createBinding


  /// <summary>
  ///   Creates a binding to a sequence of sub-models, each uniquely identified
  ///   by the value returned by <paramref name="getId" />. The sub-models have
  ///   their own bindings. You typically bind this to the <c>ItemsSource</c> of
  ///   an
  ///   <c>ItemsControl</c>, <c>ListView</c>, <c>TreeView</c>, etc.
  /// </summary>
  /// <param name="getSubModels">Gets the sub-models from the model.</param>
  /// <param name="getId">Gets a unique identifier for a sub-model.</param>
  /// <param name="bindings">Returns the bindings for the sub-model.</param>
  static member subModelSeq
      (getSubModels: 'model -> #seq<'subModel>,
       getId: 'subModel -> 'id,
       bindings: unit -> Binding<'model * 'subModel, 'msg> list)
      : string -> Binding<'model, 'msg> =
    { GetModels = fun m ->
        getSubModels m
        |> Seq.map (fun sub -> (m, sub))
      GetId = snd >> getId
      GetBindings = bindings
      ToMsg = fun _ (_, msg) -> msg }
    |> SubModelSeqData.box
    |> SubModelSeqData
    |> createBinding


  /// <summary>
  ///   Creates a two-way binding to a <c>SelectedItem</c>-like property where
  ///   the
  ///   <c>ItemsSource</c>-like property is a <see cref="subModelSeq" />
  ///   binding. Automatically converts the dynamically created Elmish.WPF view
  ///   models to/from their corresponding IDs, so the Elmish user code only has
  ///   to work with the IDs.
  ///
  ///   Only use this if you are unable to use some kind of <c>SelectedValue</c>
  ///   or
  ///   <c>SelectedIndex</c> property with a normal <see cref="twoWay" />
  ///   binding. This binding is less type-safe. It will throw when initializing
  ///   the bindings if <paramref name="subModelSeqBindingName" />
  ///   does not correspond to a <see cref="subModelSeq" /> binding, and it will
  ///   throw at runtime if if the inferred <c>'id</c> type does not match the
  ///   actual ID type used in that binding.
  /// </summary>
  /// <param name="subModelSeqBindingName">
  ///   The name of the <see cref="subModelSeq" /> binding used as the items
  ///   source.
  /// </param>
  /// <param name="get">Gets the selected sub-model/sub-binding ID from the
  /// model.</param>
  /// <param name="set">
  ///   Returns the message to dispatch on selections/de-selections.
  /// </param>
  static member subModelSelectedItem
      (subModelSeqBindingName: string,
       get: 'model -> 'id voption,
       set: 'id voption -> 'model -> 'msg)
      : string -> Binding<'model, 'msg> =
    { Get = get
      Set = set
      SubModelSeqBindingName = subModelSeqBindingName }
    |> SubModelSelectedItemData.box
    |> SubModelSelectedItemData
    |> createBinding


  /// <summary>
  ///   Creates a two-way binding to a <c>SelectedItem</c>-like property where
  ///   the
  ///   <c>ItemsSource</c>-like property is a <see cref="subModelSeq" />
  ///   binding. Automatically converts the dynamically created Elmish.WPF view
  ///   models to/from their corresponding IDs, so the Elmish user code only has
  ///   to work with the IDs.
  ///
  ///   Only use this if you are unable to use some kind of <c>SelectedValue</c>
  ///   or
  ///   <c>SelectedIndex</c> property with a normal <see cref="twoWay" />
  ///   binding. This binding is less type-safe. It will throw when initializing
  ///   the bindings if <paramref name="subModelSeqBindingName" />
  ///   does not correspond to a <see cref="subModelSeq" /> binding, and it will
  ///   throw at runtime if if the inferred <c>'id</c> type does not match the
  ///   actual ID type used in that binding.
  /// </summary>
  /// <param name="subModelSeqBindingName">
  ///   The name of the <see cref="subModelSeq" /> binding used as the items
  ///   source.
  /// </param>
  /// <param name="get">Gets the selected sub-model/sub-binding ID from the
  /// model.</param>
  /// <param name="set">
  ///   Returns the message to dispatch on selections/de-selections.
  /// </param>
  static member subModelSelectedItem
      (subModelSeqBindingName: string,
       get: 'model -> 'id option,
       set: 'id option -> 'model -> 'msg)
      : string -> Binding<'model, 'msg> =
    { Get = get >> ValueOption.ofOption
      Set = ValueOption.toOption >> set
      SubModelSeqBindingName = subModelSeqBindingName }
    |> SubModelSelectedItemData.box
    |> SubModelSelectedItemData
    |> createBinding



// Some members are implemented as extensions to help overload resolution
[<AutoOpen>]
module Extensions =

  type Binding with

    /// <summary>Creates a two-way binding.</summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    static member twoWay
        (get: 'model -> 'a,
         set: 'a -> 'msg)
        : string -> Binding<'model, 'msg> =
      TwoWayData {
        Get = get >> box
        Set = fun p _ -> p |> unbox<'a> |> set
      } |> createBinding


    /// <summary>
    ///   Creates a two-way binding to an optional value. The binding
    ///   automatically converts between the optional source value and an
    ///   unwrapped (possibly <c>null</c>) value on the view side.
    /// </summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    static member twoWayOpt
        (get: 'model -> 'a option,
         set: 'a option -> 'msg)
        : string -> Binding<'model, 'msg> =
      TwoWayData {
        Get = get >> Option.map box >> Option.toObj
        Set = fun p _ -> p |> Option.ofObj |> Option.map unbox<'a> |> set
      } |> createBinding


    /// <summary>
    ///   Creates a two-way binding to an optional value. The binding
    ///   automatically converts between the optional source value and an
    ///   unwrapped (possibly <c>null</c>) value on the view side.
    /// </summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    static member twoWayOpt
        (get: 'model -> 'a voption,
         set: 'a voption -> 'msg)
        : string -> Binding<'model, 'msg> =
      TwoWayData {
        Get = get >> ValueOption.map box >> ValueOption.toObj
        Set = fun p _ -> p |> ValueOption.ofObj |> ValueOption.map unbox<'a> |> set
      } |> createBinding


    /// <summary>
    ///   Creates a two-way binding with validation using
    ///   <c>INotifyDataErrorInfo</c>.
    /// </summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    /// <param name="validate">
    ///   Returns the validation messages from the updated model.
    /// </param>
    static member twoWayValidate
        (get: 'model -> 'a,
         set: 'a -> 'msg,
         validate: 'model -> string list)
        : string -> Binding<'model, 'msg> =
      { Get = get
        Set = fun p _ -> set p
        Validate = validate }
      |> TwoWayValidateData.box
      |> TwoWayValidateData
      |> createBinding


    /// <summary>
    ///   Creates a two-way binding with validation using
    ///   <c>INotifyDataErrorInfo</c>.
    /// </summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    /// <param name="validate">
    ///   Returns the validation message from the updated model.
    /// </param>
    static member twoWayValidate
        (get: 'model -> 'a,
         set: 'a -> 'msg,
         validate: 'model -> string voption)
        : string -> Binding<'model, 'msg> =
      { Get = get
        Set = fun p _ -> set p
        Validate = validate >> ValueOption.toList }
      |> TwoWayValidateData.box
      |> TwoWayValidateData
      |> createBinding


    /// <summary>
    ///   Creates a two-way binding with validation using
    ///   <c>INotifyDataErrorInfo</c>.
    /// </summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    /// <param name="validate">
    ///   Returns the validation message from the updated model.
    /// </param>
    static member twoWayValidate
        (get: 'model -> 'a,
         set: 'a -> 'msg,
         validate: 'model -> string option)
        : string -> Binding<'model, 'msg> =
      { Get = get
        Set = fun p  _ -> set p
        Validate = validate >> Option.toList }
      |> TwoWayValidateData.box
      |> TwoWayValidateData
      |> createBinding


    /// <summary>
    ///   Creates a two-way binding with validation using
    ///   <c>INotifyDataErrorInfo</c>.
    /// </summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    /// <param name="validate">
    ///   Returns the validation message from the updated model.
    /// </param>
    static member twoWayValidate
        (get: 'model -> 'a,
         set: 'a -> 'msg,
         validate: 'model -> Result<'ignored, string>)
        : string -> Binding<'model, 'msg> =
      { Get = get
        Set = fun p _ -> set p
        Validate = validate >> ValueOption.ofError >> ValueOption.toList }
      |> TwoWayValidateData.box
      |> TwoWayValidateData
      |> createBinding


    /// <summary>
    ///   Creates a two-way binding to an optional value with validation using
    ///   <c>INotifyDataErrorInfo</c>. The binding automatically converts
    ///   between the optional source value and an unwrapped (possibly
    ///   <c>null</c>) value on the view side.
    /// </summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    /// <param name="validate">
    ///   Returns the validation messages from the updated model.
    /// </param>
    static member twoWayOptValidate
        (get: 'model -> 'a voption,
         set: 'a voption -> 'msg,
         validate: 'model -> string list)
        : string -> Binding<'model, 'msg> =
      { Get = get
        Set = fun p _ -> set p
        Validate = validate }
      |> TwoWayValidateData.boxVOpt
      |> TwoWayValidateData
      |> createBinding


    /// <summary>
    ///   Creates a two-way binding to an optional value with validation using
    ///   <c>INotifyDataErrorInfo</c>. The binding automatically converts
    ///   between the optional source value and an unwrapped (possibly
    ///   <c>null</c>) value on the view side.
    /// </summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    /// <param name="validate">
    ///   Returns the validation message from the updated model.
    /// </param>
    static member twoWayOptValidate
        (get: 'model -> 'a voption,
         set: 'a voption -> 'msg,
         validate: 'model -> string voption)
        : string -> Binding<'model, 'msg> =
      { Get = get
        Set = fun p _ -> set p
        Validate = validate >> ValueOption.toList }
      |> TwoWayValidateData.boxVOpt
      |> TwoWayValidateData
      |> createBinding


    /// <summary>
    ///   Creates a two-way binding to an optional value with validation using
    ///   <c>INotifyDataErrorInfo</c>. The binding automatically converts
    ///   between the optional source value and an unwrapped (possibly
    ///   <c>null</c>) value on the view side.
    /// </summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    /// <param name="validate">
    ///   Returns the validation message from the updated model.
    /// </param>
    static member twoWayOptValidate
        (get: 'model -> 'a voption,
         set: 'a voption -> 'msg,
         validate: 'model -> string option)
        : string -> Binding<'model, 'msg> =
      { Get = get
        Set = fun p _ -> set p
        Validate = validate >> Option.toList }
      |> TwoWayValidateData.boxVOpt
      |> TwoWayValidateData
      |> createBinding


    /// <summary>
    ///   Creates a two-way binding to an optional value with validation using
    ///   <c>INotifyDataErrorInfo</c>. The binding automatically converts
    ///   between the optional source value and an unwrapped (possibly
    ///   <c>null</c>) value on the view side.
    /// </summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    /// <param name="validate">
    ///   Returns the validation message from the updated model.
    /// </param>
    static member twoWayOptValidate
        (get: 'model -> 'a voption,
         set: 'a voption -> 'msg,
         validate: 'model -> Result<'ignored, string>)
        : string -> Binding<'model, 'msg> =
      { Get = get
        Set = fun p _ -> set p
        Validate = validate >> ValueOption.ofError >> ValueOption.toList }
      |> TwoWayValidateData.boxVOpt
      |> TwoWayValidateData
      |> createBinding


    /// <summary>
    ///   Creates a two-way binding to an optional value with validation using
    ///   <c>INotifyDataErrorInfo</c>. The binding automatically converts
    ///   between the optional source value and an unwrapped (possibly
    ///   <c>null</c>) value on the view side.
    /// </summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    /// <param name="validate">
    ///   Returns the validation messages from the updated model.
    /// </param>
    static member twoWayOptValidate
        (get: 'model -> 'a option,
         set: 'a option -> 'msg,
         validate: 'model -> string list)
        : string -> Binding<'model, 'msg> =
      { Get = get
        Set = fun p _ -> set p
        Validate = validate }
      |> TwoWayValidateData.boxOpt
      |> TwoWayValidateData
      |> createBinding


    /// <summary>
    ///   Creates a two-way binding to an optional value with validation using
    ///   <c>INotifyDataErrorInfo</c>. The binding automatically converts
    ///   between the optional source value and an unwrapped (possibly
    ///   <c>null</c>) value on the view side.
    /// </summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    /// <param name="validate">
    ///   Returns the validation message from the updated model.
    /// </param>
    static member twoWayOptValidate
        (get: 'model -> 'a option,
         set: 'a option -> 'msg,
         validate: 'model -> string voption)
        : string -> Binding<'model, 'msg> =
      { Get = get
        Set = fun p _ -> set p
        Validate = validate >> ValueOption.toList }
      |> TwoWayValidateData.boxOpt
      |> TwoWayValidateData
      |> createBinding


    /// <summary>
    ///   Creates a two-way binding to an optional value with validation using
    ///   <c>INotifyDataErrorInfo</c>. The binding automatically converts
    ///   between the optional source value and an unwrapped (possibly
    ///   <c>null</c>) value on the view side.
    /// </summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    /// <param name="validate">
    ///   Returns the validation message from the updated model.
    /// </param>
    static member twoWayOptValidate
        (get: 'model -> 'a option,
         set: 'a option -> 'msg,
         validate: 'model -> string option)
        : string -> Binding<'model, 'msg> =
      { Get = get
        Set = fun p _ -> set p
        Validate = validate >> Option.toList }
      |> TwoWayValidateData.boxOpt
      |> TwoWayValidateData
      |> createBinding


    /// <summary>
    ///   Creates a two-way binding to an optional value with validation using
    ///   <c>INotifyDataErrorInfo</c>. The binding automatically converts
    ///   between the optional source value and an unwrapped (possibly
    ///   <c>null</c>) value on the view side.
    /// </summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    /// <param name="validate">
    ///   Returns the validation message from the updated model.
    /// </param>
    static member twoWayOptValidate
        (get: 'model -> 'a option,
         set: 'a option -> 'msg,
         validate: 'model -> Result<'ignored, string>)
        : string -> Binding<'model, 'msg> =
      { Get = get
        Set = fun p _ -> set p
        Validate = validate >> ValueOption.ofError >> ValueOption.toList }
      |> TwoWayValidateData.boxOpt
      |> TwoWayValidateData
      |> createBinding


    /// <summary>
    ///   Creates a <c>Command</c> binding that dispatches the specified message
    ///   and can always execute.
    /// </summary>
    /// <param name="exec">Returns the message to dispatch.</param>
    static member cmd
        (exec: 'msg)
        : string -> Binding<'model, 'msg> =
      CmdData {
        Exec = fun _ -> exec |> ValueSome
        CanExec = fun _ -> true
      } |> createBinding


    /// <summary>
    ///   Creates a <c>Command</c> binding that dispatches the specified message
    ///   and can execute if <paramref name="canExec" /> returns <c>true</c>.
    /// </summary>
    /// <param name="exec">Returns the message to dispatch.</param>
    /// <param name="canExec">Indicates whether the command can execute.</param>
    static member cmdIf
        (exec: 'msg,
         canExec: 'model -> bool)
        : string -> Binding<'model, 'msg> =
      CmdData {
        Exec = fun _ -> exec |> ValueSome
        CanExec = canExec
      } |> createBinding


    /// <summary>
    ///   Creates a <c>Command</c> binding that depends on the
    ///   <c>CommandParameter</c>
    ///   and can always execute.
    /// </summary>
    /// <param name="exec">Returns the message to dispatch.</param>
    static member cmdParam
        (exec: obj -> 'msg)
        : string -> Binding<'model, 'msg> =
      CmdParamData {
        Exec = fun p _ -> exec p |> ValueSome
        CanExec = fun _ _ -> true
        AutoRequery = false
      } |> createBinding


    /// <summary>
    ///   Creates a conditional <c>Command</c> binding that depends on the
    ///   <c>CommandParameter</c>
    ///   and can execute if <paramref name="exec" /> returns <c>ValueSome</c>.
    /// </summary>
    /// <param name="exec">Returns the message to dispatch.</param>
    /// <param name="uiBoundCmdParam">
    ///   If <c>true</c>, <c>CanExecuteChanged</c> will trigger every time WPF's
    ///   <c>CommandManager</c>
    ///   detects UI changes that could potentially influence the command's
    ///   ability to execute. This will likely lead to many more triggers than
    ///   necessary, but is needed if you have bound the <c>CommandParameter</c>
    ///   to another UI property.
    /// </param>
    static member cmdParamIf
        (exec: obj -> 'msg voption,
         ?uiBoundCmdParam: bool)
        : string -> Binding<'model, 'msg> =
      CmdParamData {
        Exec = fun p _ -> exec p
        CanExec = fun p _ -> exec p |> ValueOption.isSome
        AutoRequery = defaultArg uiBoundCmdParam false
      } |> createBinding


    /// <summary>
    ///   Creates a conditional <c>Command</c> binding that depends on the
    ///   <c>CommandParameter</c>
    ///   and can execute if <paramref name="exec" /> returns <c>Some</c>.
    /// </summary>
    /// <param name="exec">Returns the message to dispatch.</param>
    /// <param name="uiBoundCmdParam">
    ///   If <c>true</c>, <c>CanExecuteChanged</c> will trigger every time WPF's
    ///   <c>CommandManager</c>
    ///   detects UI changes that could potentially influence the command's
    ///   ability to execute. This will likely lead to many more triggers than
    ///   necessary, but is needed if you have bound the <c>CommandParameter</c>
    ///   to another UI property.
    /// </param>
    static member cmdParamIf
        (exec: obj -> 'msg option,
         ?uiBoundCmdParam: bool)
        : string -> Binding<'model, 'msg> =
      CmdParamData {
        Exec = fun p _ -> exec p |> ValueOption.ofOption
        CanExec = fun p _ -> exec p |> Option.isSome
        AutoRequery = defaultArg uiBoundCmdParam false
      } |> createBinding


    /// <summary>
    ///   Creates a conditional <c>Command</c> binding that depends on the
    ///   <c>CommandParameter</c>
    ///   and can execute if <paramref name="exec" /> returns <c>Ok</c>.
    ///
    ///   This overload allows more easily re-using the same validation
    ///   functions for inputs and commands.
    /// </summary>
    /// <param name="exec">Returns the message to dispatch.</param>
    /// <param name="uiBoundCmdParam">
    ///   If <c>true</c>, <c>CanExecuteChanged</c> will trigger every time WPF's
    ///   <c>CommandManager</c>
    ///   detects UI changes that could potentially influence the command's
    ///   ability to execute. This will likely lead to many more triggers than
    ///   necessary, but is needed if you have bound the <c>CommandParameter</c>
    ///   to another UI property.
    /// </param>
    static member cmdParamIf
        (exec: obj -> Result<'msg, 'ignored>,
         ?uiBoundCmdParam: bool)
        : string -> Binding<'model, 'msg> =
      CmdParamData {
        Exec = fun p _ -> exec p |> ValueOption.ofOk
        CanExec = fun p _ -> exec p |> Result.isOk
        AutoRequery = defaultArg uiBoundCmdParam false
      } |> createBinding


    /// <summary>
    ///   Creates a <c>Command</c> binding that depends on the
    ///   <c>CommandParameter</c>
    ///   and can execute if <paramref name="canExec" /> returns <c>true</c>.
    /// </summary>
    /// <param name="exec">Returns the message to dispatch.</param>
    /// <param name="canExec">Indicates whether the command can execute.</param>
    /// <param name="uiBoundCmdParam">
    ///   If <c>true</c>, <c>CanExecuteChanged</c> will trigger every time WPF's
    ///   <c>CommandManager</c>
    ///   detects UI changes that could potentially influence the command's
    ///   ability to execute. This will likely lead to many more triggers than
    ///   necessary, but is needed if you have bound the <c>CommandParameter</c>
    ///   to another UI property.
    /// </param>
    static member cmdParamIf
        (exec: obj -> 'msg,
         canExec: obj -> bool,
         ?uiBoundCmdParam: bool)
        : string -> Binding<'model, 'msg> =
      CmdParamData {
        Exec = fun p _ -> exec p |> ValueSome
        CanExec = fun p _ -> canExec p
        AutoRequery = defaultArg uiBoundCmdParam false
      } |> createBinding


    /// <summary>
    ///   Creates a two-way binding to a <c>SelectedItem</c>-like property where
    ///   the
    ///   <c>ItemsSource</c>-like property is a <see cref="subModelSeq" />
    ///   binding. Automatically converts the dynamically created Elmish.WPF
    ///   view models to/from their corresponding IDs, so the Elmish user code
    ///   only has to work with the IDs.
    ///
    ///   Only use this if you are unable to use some kind of
    ///   <c>SelectedValue</c> or
    ///   <c>SelectedIndex</c> property with a normal <see cref="twoWay" />
    ///   binding. This binding is less type-safe. It will throw when
    ///   initializing the bindings if <paramref name="subModelSeqBindingName"
    ///   />
    ///   does not correspond to a <see cref="subModelSeq" /> binding, and it
    ///   will throw at runtime if if the inferred <c>'id</c> type does not
    ///   match the actual ID type used in that binding.
    /// </summary>
    /// <param name="subModelSeqBindingName">
    ///   The name of the <see cref="subModelSeq" /> binding used as the items
    ///   source.
    /// </param>
    /// <param name="get">Gets the selected sub-model/sub-binding ID from the
    /// model.</param>
    /// <param name="set">
    ///   Returns the message to dispatch on selections/de-selections.
    /// </param>
    static member subModelSelectedItem
        (subModelSeqBindingName: string,
         get: 'model -> 'id voption,
         set: 'id voption -> 'msg)
        : string -> Binding<'model, 'msg> =
      SubModelSelectedItemData {
        Get = get >> ValueOption.map box
        Set = fun id _ -> id |> ValueOption.map unbox<'id> |> set
        SubModelSeqBindingName = subModelSeqBindingName
      } |> createBinding


    /// <summary>
    ///   Creates a two-way binding to a <c>SelectedItem</c>-like property where
    ///   the
    ///   <c>ItemsSource</c>-like property is a <see cref="subModelSeq" />
    ///   binding. Automatically converts the dynamically created Elmish.WPF
    ///   view models to/from their corresponding IDs, so the Elmish user code
    ///   only has to work with the IDs.
    ///
    ///   Only use this if you are unable to use some kind of
    ///   <c>SelectedValue</c> or
    ///   <c>SelectedIndex</c> property with a normal <see cref="twoWay" />
    ///   binding. This binding is less type-safe. It will throw when
    ///   initializing the bindings if <paramref name="subModelSeqBindingName"
    ///   />
    ///   does not correspond to a <see cref="subModelSeq" /> binding, and it
    ///   will throw at runtime if if the inferred <c>'id</c> type does not
    ///   match the actual ID type used in that binding.
    /// </summary>
    /// <param name="subModelSeqBindingName">
    ///   The name of the <see cref="subModelSeq" /> binding used as the items
    ///   source.
    /// </param>
    /// <param name="get">Gets the selected sub-model/sub-binding ID from the
    /// model.</param>
    /// <param name="set">
    ///   Returns the message to dispatch on selections/de-selections.
    /// </param>
    static member subModelSelectedItem
        (subModelSeqBindingName: string,
         get: 'model -> 'id option,
         set: 'id option -> 'msg)
        : string -> Binding<'model, 'msg> =
      SubModelSelectedItemData {
        Get = get >> ValueOption.ofOption >> ValueOption.map box
        Set = fun id _ -> id |> ValueOption.map unbox<'id> |> ValueOption.toOption |> set
        SubModelSeqBindingName = subModelSeqBindingName
      } |> createBinding
