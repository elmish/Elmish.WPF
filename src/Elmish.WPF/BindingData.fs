[<AutoOpen>]
module internal Elmish.WPF.BindingData

open System.Collections.ObjectModel
open System.Windows

open Elmish


module Helper =

  let mapDispatch
      (getCurrentModel: unit -> 'model)
      (set: 'bindingMsg -> 'model -> 'msg)
      (dispatch: 'msg -> unit)
      : 'bindingMsg -> unit =
    fun bMsg -> getCurrentModel () |> set bMsg |> dispatch


type OneWayData<'model, 'a> =
  { Get: 'model -> 'a }


type OneWayToSourceData<'model, 'msg, 'a> =
  { Set: 'a -> 'model -> 'msg }


type OneWaySeqData<'model, 'a, 'aCollection, 'id when 'id : equality> =
  { Get: 'model -> 'a seq
    CreateCollection: 'a seq -> CollectionTarget<'a, 'aCollection>
    GetId: 'a -> 'id
    ItemEquals: 'a -> 'a -> bool }

  member d.Merge(values: CollectionTarget<'a, 'aCollection>, newModel: 'model) =
    let create v _ = v
    let update oldVal newVal oldIdx =
      if not (d.ItemEquals newVal oldVal) then
        values.SetAt (oldIdx, newVal)
    let newVals = newModel |> d.Get |> Seq.toArray
    Merge.keyed d.GetId d.GetId create update values newVals


type TwoWayData<'model, 'msg, 'a> =
  { Get: 'model -> 'a
    Set: 'a -> 'model -> 'msg }


type CmdData<'model, 'msg> = {
  Exec: obj -> 'model -> 'msg voption
  CanExec: obj -> 'model -> bool
  AutoRequery: bool
}


type SubModelSelectedItemData<'model, 'msg, 'id> =
  { Get: 'model -> 'id voption
    Set: 'id voption -> 'model -> 'msg
    SubModelSeqBindingName: string }


type SubModelData<'model, 'msg, 'bindingModel, 'bindingMsg, 'vm> = {
  GetModel: 'model -> 'bindingModel voption
  CreateViewModel: ViewModelArgs<'bindingModel, 'bindingMsg> -> 'vm
  UpdateViewModel: 'vm * 'bindingModel -> unit
  ToMsg: 'model -> 'bindingMsg -> 'msg
}


and SubModelWinData<'model, 'msg, 'bindingModel, 'bindingMsg, 'vm> = {
  GetState: 'model -> WindowState<'bindingModel>
  CreateViewModel: ViewModelArgs<'bindingModel, 'bindingMsg> -> 'vm
  UpdateViewModel: 'vm * 'bindingModel -> unit
  ToMsg: 'model -> 'bindingMsg -> 'msg
  GetWindow: 'model -> Dispatch<'msg> -> Window
  IsModal: bool
  OnCloseRequested: 'model -> 'msg voption
}


and SubModelSeqUnkeyedData<'model, 'msg, 'bindingModel, 'bindingMsg, 'vm, 'vmCollection> =
  { GetModels: 'model -> 'bindingModel seq
    CreateViewModel: ViewModelArgs<'bindingModel, 'bindingMsg> -> 'vm
    CreateCollection: 'vm seq -> CollectionTarget<'vm, 'vmCollection>
    UpdateViewModel: 'vm * 'bindingModel -> unit
    ToMsg: 'model -> int * 'bindingMsg -> 'msg }


and SubModelSeqKeyedData<'model, 'msg, 'bindingModel, 'bindingMsg, 'vm, 'vmCollection, 'id when 'id : equality> =
  { GetSubModels: 'model -> 'bindingModel seq
    CreateViewModel: ViewModelArgs<'bindingModel, 'bindingMsg> -> 'vm
    CreateCollection: 'vm seq -> CollectionTarget<'vm, 'vmCollection>
    UpdateViewModel: 'vm * 'bindingModel -> unit
    ToMsg: 'model -> 'id * 'bindingMsg -> 'msg
    BmToId: 'bindingModel -> 'id
    VmToId: 'vm -> 'id }

  member d.MergeKeyed
      (create: 'bindingModel -> 'id -> 'vm,
       update: 'vm -> 'bindingModel -> unit,
       values: CollectionTarget<'vm, 'vmCollection>,
       newSubModels: 'bindingModel []) =
    let update vm bm _ = update vm bm
    Merge.keyed d.BmToId d.VmToId create update values newSubModels


and ValidationData<'model, 'msg, 't> =
  { BindingData: BindingData<'model, 'msg, 't>
    Validate: 'model -> string list }


and LazyData<'model, 'msg, 'bindingModel, 'bindingMsg, 't> =
  { BindingData: BindingData<'bindingModel, 'bindingMsg, 't>
    Get: 'model -> 'bindingModel
    Set: 'bindingMsg -> 'model -> 'msg
    Equals: 'bindingModel -> 'bindingModel -> bool }

  member this.MapDispatch
      (getCurrentModel: unit -> 'model,
       dispatch: 'msg -> unit)
       : 'bindingMsg -> unit =
    Helper.mapDispatch getCurrentModel this.Set dispatch


and AlterMsgStreamData<'model, 'msg, 'bindingModel, 'bindingMsg, 'dispatchMsg, 't> =
 { BindingData: BindingData<'bindingModel, 'bindingMsg, 't>
   Get: 'model -> 'bindingModel
   Set: 'dispatchMsg -> 'model -> 'msg
   AlterMsgStream: ('dispatchMsg -> unit) -> 'bindingMsg -> unit }

  member this.MapDispatch
      (getCurrentModel: unit -> 'model,
       dispatch: 'msg -> unit)
       : 'bindingMsg -> unit =
    Helper.mapDispatch getCurrentModel this.Set dispatch
    |> this.AlterMsgStream


and BaseBindingData<'model, 'msg, 't> =
  | OneWayData of OneWayData<'model, 't>
  | OneWayToSourceData of OneWayToSourceData<'model, 'msg, 't>
  | OneWaySeqData of OneWaySeqData<'model, obj, 't, obj>
  | TwoWayData of TwoWayData<'model, 'msg, 't>
  | CmdData of CmdData<'model, 'msg>
  | SubModelData of SubModelData<'model, 'msg, obj, obj, 't>
  | SubModelWinData of SubModelWinData<'model, 'msg, obj, obj, 't>
  | SubModelSeqUnkeyedData of SubModelSeqUnkeyedData<'model, 'msg, obj, obj, obj, 't>
  | SubModelSeqKeyedData of SubModelSeqKeyedData<'model, 'msg, obj, obj, obj, 't, obj>
  | SubModelSelectedItemData of SubModelSelectedItemData<'model, 'msg, obj>


and BindingData<'model, 'msg, 't> =
  | BaseBindingData of BaseBindingData<'model, 'msg, 't>
  | CachingData of BindingData<'model, 'msg, 't>
  | ValidationData of ValidationData<'model, 'msg, 't>
  | LazyData of LazyData<'model, 'msg, obj, obj, 't>
  | AlterMsgStreamData of AlterMsgStreamData<'model, 'msg, obj, obj, obj, 't>



module BindingData =

  module private MapT =

    let baseCase (fOut: 't0 -> 't1) (fIn: 't1 -> 't0) =
      function
      | OneWayData d -> OneWayData {
          Get = d.Get >> fOut
        }
      | OneWayToSourceData d -> OneWayToSourceData {
          Set = fIn >> d.Set
        }
      | OneWaySeqData d -> OneWaySeqData {
          Get = d.Get
          CreateCollection = d.CreateCollection >> CollectionTarget.mapCollection fOut
          GetId = d.GetId
          ItemEquals = d.ItemEquals
        }
      | TwoWayData d -> TwoWayData {
          Get = d.Get >> fOut
          Set = fIn >> d.Set
        }
      | CmdData d -> CmdData {
          Exec = d.Exec
          CanExec = d.CanExec
          AutoRequery = d.AutoRequery
        }
      | SubModelData d -> SubModelData {
          GetModel = d.GetModel
          CreateViewModel = d.CreateViewModel >> fOut
          UpdateViewModel = (fun (vm,m) -> d.UpdateViewModel (fIn vm, m))
          ToMsg = d.ToMsg
        }
      | SubModelWinData d -> SubModelWinData {
          GetState = d.GetState
          CreateViewModel = d.CreateViewModel >> fOut
          UpdateViewModel = (fun (vm,m) -> d.UpdateViewModel (fIn vm, m))
          ToMsg = d.ToMsg
          GetWindow = d.GetWindow
          IsModal = d.IsModal
          OnCloseRequested = d.OnCloseRequested
        }
      | SubModelSeqUnkeyedData d -> SubModelSeqUnkeyedData {
          GetModels = d.GetModels
          CreateViewModel = d.CreateViewModel
          CreateCollection = d.CreateCollection >> CollectionTarget.mapCollection fOut
          UpdateViewModel = d.UpdateViewModel
          ToMsg = d.ToMsg
        }
      | SubModelSeqKeyedData d -> SubModelSeqKeyedData {
          GetSubModels = d.GetSubModels
          CreateViewModel = d.CreateViewModel
          CreateCollection = d.CreateCollection >> CollectionTarget.mapCollection fOut
          UpdateViewModel = d.UpdateViewModel
          ToMsg = d.ToMsg
          VmToId = d.VmToId
          BmToId = d.BmToId
        }
      | SubModelSelectedItemData d -> SubModelSelectedItemData {
          Get = d.Get
          Set = d.Set
          SubModelSeqBindingName = d.SubModelSeqBindingName
        }

    let rec recursiveCase<'model, 'msg, 't0, 't1> (fOut: 't0 -> 't1) (fIn: 't1 -> 't0)
      : BindingData<'model, 'msg, 't0> -> BindingData<'model, 'msg, 't1> =
      function
      | BaseBindingData d -> d |> baseCase fOut fIn |> BaseBindingData
      | CachingData d -> d |> recursiveCase<'model, 'msg, 't0, 't1> fOut fIn |> CachingData
      | ValidationData d -> ValidationData {
          BindingData = recursiveCase<'model, 'msg, 't0, 't1> fOut fIn d.BindingData
          Validate = d.Validate
        }
      | LazyData d -> LazyData {
          Get = d.Get
          Set = d.Set
          BindingData = recursiveCase<obj, obj, 't0, 't1> fOut fIn d.BindingData
          Equals = d.Equals
        }
      | AlterMsgStreamData d -> AlterMsgStreamData {
          BindingData = recursiveCase<obj, obj, 't0, 't1> fOut fIn d.BindingData
          AlterMsgStream = d.AlterMsgStream
          Get = d.Get
          Set = d.Set
        }

  let boxT b = MapT.recursiveCase box unbox b

  let mapModel f =
    let binaryHelper binary x m = binary x (f m)
    let baseCase = function
      | OneWayData d -> OneWayData {
          Get = f >> d.Get
        }
      | OneWayToSourceData d -> OneWayToSourceData {
          Set = binaryHelper d.Set
        }
      | OneWaySeqData d -> OneWaySeqData {
          Get = f >> d.Get
          CreateCollection = d.CreateCollection
          GetId = d.GetId
          ItemEquals = d.ItemEquals
        }
      | TwoWayData d -> TwoWayData {
          Get = f >> d.Get
          Set = binaryHelper d.Set
        }
      | CmdData d -> CmdData {
          Exec = binaryHelper d.Exec
          CanExec = binaryHelper d.CanExec
          AutoRequery = d.AutoRequery
        }
      | SubModelData d -> SubModelData {
          GetModel = f >> d.GetModel
          CreateViewModel = d.CreateViewModel
          UpdateViewModel = d.UpdateViewModel
          ToMsg = f >> d.ToMsg
        }
      | SubModelWinData d -> SubModelWinData {
          GetState = f >> d.GetState
          CreateViewModel = d.CreateViewModel
          UpdateViewModel = d.UpdateViewModel
          ToMsg = f >> d.ToMsg
          GetWindow = f >> d.GetWindow
          IsModal = d.IsModal
          OnCloseRequested = f >> d.OnCloseRequested
        }
      | SubModelSeqUnkeyedData d -> SubModelSeqUnkeyedData {
          GetModels = f >> d.GetModels
          CreateViewModel = d.CreateViewModel
          CreateCollection = d.CreateCollection
          UpdateViewModel = d.UpdateViewModel
          ToMsg = f >> d.ToMsg
        }
      | SubModelSeqKeyedData d -> SubModelSeqKeyedData {
          GetSubModels = f >> d.GetSubModels
          CreateViewModel = d.CreateViewModel
          CreateCollection = d.CreateCollection
          UpdateViewModel = d.UpdateViewModel
          ToMsg = f >> d.ToMsg
          BmToId = d.BmToId
          VmToId = d.VmToId
        }
      | SubModelSelectedItemData d -> SubModelSelectedItemData {
          Get = f >> d.Get
          Set = binaryHelper d.Set
          SubModelSeqBindingName = d.SubModelSeqBindingName
        }
    let rec recursiveCase = function
      | BaseBindingData d -> d |> baseCase |> BaseBindingData
      | CachingData d -> d |> recursiveCase |> CachingData
      | ValidationData d -> ValidationData {
          BindingData = recursiveCase d.BindingData
          Validate = f >> d.Validate
        }
      | LazyData d -> LazyData {
          BindingData = d.BindingData
          Get = f >> d.Get
          Set = binaryHelper d.Set
          Equals = d.Equals
        }
      | AlterMsgStreamData d -> AlterMsgStreamData {
          BindingData = d.BindingData
          AlterMsgStream = d.AlterMsgStream
          Get = f >> d.Get
          Set = binaryHelper d.Set
        }
    recursiveCase

  let mapMsgWithModel (f: 'a -> 'model -> 'b) =
    let baseCase = function
      | OneWayData d -> d |> OneWayData
      | OneWayToSourceData d -> OneWayToSourceData {
          Set = fun v m -> f (d.Set v m) m
        }
      | OneWaySeqData d -> d |> OneWaySeqData
      | TwoWayData d -> TwoWayData {
          Get = d.Get
          Set = fun v m -> f (d.Set v m) m
        }
      | CmdData d -> CmdData {
          Exec = fun p m -> d.Exec p m |> ValueOption.map (fun msg -> f msg m)
          CanExec = fun p m -> d.CanExec p m
          AutoRequery = d.AutoRequery
        }
      | SubModelData d -> SubModelData {
          GetModel = d.GetModel
          CreateViewModel = d.CreateViewModel
          UpdateViewModel = d.UpdateViewModel
          ToMsg = fun m bMsg -> f (d.ToMsg m bMsg) m
        }
      | SubModelWinData d -> SubModelWinData {
          GetState = d.GetState
          CreateViewModel = d.CreateViewModel
          UpdateViewModel = d.UpdateViewModel
          ToMsg = fun m bMsg -> f (d.ToMsg m bMsg) m
          GetWindow = fun m dispatch -> d.GetWindow m (fun msg -> f msg m |> dispatch)
          IsModal = d.IsModal
          OnCloseRequested = fun m -> m |> d.OnCloseRequested |> ValueOption.map (fun msg -> f msg m)
        }
      | SubModelSeqUnkeyedData d -> SubModelSeqUnkeyedData {
          GetModels = d.GetModels
          CreateViewModel = d.CreateViewModel
          CreateCollection = d.CreateCollection
          UpdateViewModel = d.UpdateViewModel
          ToMsg = fun m bMsg -> f (d.ToMsg m bMsg) m
        }
      | SubModelSeqKeyedData d -> SubModelSeqKeyedData {
          GetSubModels = d.GetSubModels
          CreateViewModel = d.CreateViewModel
          CreateCollection = d.CreateCollection
          UpdateViewModel = d.UpdateViewModel
          ToMsg = fun m bMsg -> f (d.ToMsg m bMsg) m
          BmToId = d.BmToId
          VmToId = d.VmToId
        }
      | SubModelSelectedItemData d -> SubModelSelectedItemData {
          Get = d.Get
          Set = fun v m -> f (d.Set v m) m
          SubModelSeqBindingName = d.SubModelSeqBindingName
        }
    let rec recursiveCase = function
      | BaseBindingData d -> d |> baseCase |> BaseBindingData
      | CachingData d -> d |> recursiveCase |> CachingData
      | ValidationData d -> ValidationData {
          BindingData = recursiveCase d.BindingData
          Validate = d.Validate
        }
      | LazyData d ->
      LazyData {
          BindingData = d.BindingData
          Get = d.Get
          Set = fun a m -> f (d.Set a m) m
          Equals = d.Equals
        }
      | AlterMsgStreamData d -> AlterMsgStreamData {
          BindingData = d.BindingData
          Get = d.Get
          Set = fun a m -> f (d.Set a m) m
          AlterMsgStream = d.AlterMsgStream
        }
    recursiveCase

  let mapMsg f = mapMsgWithModel (fun a _ -> f a)

  let setMsgWithModel f = mapMsgWithModel (fun _ m -> f m)
  let setMsg msg = mapMsg (fun _ -> msg)

  let addCaching b = b |> CachingData
  let addValidation validate b = { BindingData = b; Validate = validate } |> ValidationData
  let addLazy (equals: 'model -> 'model -> bool) b =
      { BindingData = b |> mapModel unbox |> mapMsg box
        Get = box
        Set = fun (dMsg: obj) _ -> unbox dMsg
        Equals = fun m1 m2 -> equals (unbox m1) (unbox m2)
      } |> LazyData
  let alterMsgStream
      (alteration: ('dispatchMsg -> unit) -> 'bindingMsg -> unit)
      (b: BindingData<'bindingModel, 'bindingMsg, 't>)
      : BindingData<'model, 'msg, 't> =
    { BindingData = b |> mapModel unbox |> mapMsg box
      Get = box
      Set = fun (dMsg: obj) _ -> unbox dMsg
      AlterMsgStream =
        fun (f: obj -> unit) ->
          let f' = box >> f
          let g = alteration f'
          unbox >> g
    } |> AlterMsgStreamData
  let addSticky (predicate: 'model -> bool) (binding: BindingData<'model, 'msg, 't>) =
    let mutable stickyModel = None
    let f newModel =
      if predicate newModel then
        stickyModel <- Some newModel
        newModel
      else
        stickyModel |> Option.defaultValue newModel
    binding |> mapModel f


  module Option =

    let box ma = ma |> Option.map box |> Option.toObj
    let unbox obj = obj |> Option.ofObj |> Option.map unbox

  module ValueOption =

    let box ma = ma |> ValueOption.map box |> ValueOption.toObj
    let unbox obj = obj |> ValueOption.ofObj |> ValueOption.map unbox


  module OneWay =

    let id<'a, 'msg> : BindingData<'a, 'msg, 'a> =
      { Get = id }
      |> OneWayData
      |> BaseBindingData

    let private mapFunctions
        mGet
        (d: OneWayData<'model, 'a>) =
      { d with Get = mGet d.Get }

    let measureFunctions
        mGet =
      mapFunctions
        (mGet "get")


  module OneWayToSource =

    let id<'model, 'a> : BindingData<'model, 'a, 'a> =
      { OneWayToSourceData.Set = Func2.id1 }
      |> OneWayToSourceData
      |> BaseBindingData

    let private mapFunctions
        mSet
        (d: OneWayToSourceData<'model, 'msg, 'a>) =
      { d with Set = mSet d.Set }

    let measureFunctions
        mSet =
      mapFunctions
        (mSet "set")


  module OneWaySeq =

    let mapMinorTypes
        (outMapA: 'a -> 'a0)
        (outMapId: 'id -> 'id0)
        (inMapA: 'a0 -> 'a)
        (d: OneWaySeqData<'model, 'a, 'aCollection, 'id>) = {
      Get = d.Get >> Seq.map outMapA
      CreateCollection = Seq.map inMapA >> d.CreateCollection >> CollectionTarget.mapA outMapA inMapA
      GetId = inMapA >> d.GetId >> outMapId
      ItemEquals = fun a1 a2 -> d.ItemEquals (inMapA a1) (inMapA a2)
    }

    let boxMinorTypes d = d |> mapMinorTypes box box unbox

    let create itemEquals getId =
      { Get = (fun x -> upcast x)
        CreateCollection = ObservableCollection >> CollectionTarget.create
        ItemEquals = itemEquals
        GetId = getId }
      |> boxMinorTypes
      |> OneWaySeqData
      |> BaseBindingData

    let private mapFunctions
        mGet
        mGetId
        mItemEquals
        (d: OneWaySeqData<'model, 'a, 'aCollection, 'id>) =
      { d with Get = mGet d.Get
               GetId = mGetId d.GetId
               ItemEquals = mItemEquals d.ItemEquals }

    let measureFunctions
        mGet
        mGetId
        mItemEquals =
      mapFunctions
        (mGet "get")
        (mGetId "getId")
        (mItemEquals "itemEquals")


  module TwoWay =

    let id<'a> : BindingData<'a, 'a, 'a> =
      { TwoWayData.Get = id
        Set = Func2.id1 }
      |> TwoWayData
      |> BaseBindingData

    let private mapFunctions
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


  module Cmd =

    let createWithParam exec canExec autoRequery : BindingData<'model, 'msg, 't> =
      { Exec = exec
        CanExec = canExec
        AutoRequery = autoRequery }
      |> CmdData
      |> BaseBindingData

    let private mapFunctions
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


  module SubModelSelectedItem =

    let mapMinorTypes
        (outMapId: 'id -> 'id0)
        (inMapId: 'id0 -> 'id)
        (d: SubModelSelectedItemData<'model, 'msg, 'id>) = {
      Get = d.Get >> ValueOption.map outMapId
      Set = ValueOption.map inMapId >> d.Set
      SubModelSeqBindingName = d.SubModelSeqBindingName
    }

    let boxMinorTypes d = d |> mapMinorTypes box unbox

    let create subModelSeqBindingName =
      { Get = id
        Set = Func2.id1
        SubModelSeqBindingName = subModelSeqBindingName }
      |> boxMinorTypes
      |> SubModelSelectedItemData
      |> BaseBindingData

    let private mapFunctions
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


  module SubModel =

    let mapMinorTypes
        (outMapBindingModel: 'bindingModel -> 'bindingModel0)
        (outMapBindingMsg: 'bindingMsg -> 'bindingMsg0)
        (inMapBindingModel: 'bindingModel0 -> 'bindingModel)
        (inMapBindingMsg: 'bindingMsg0 -> 'bindingMsg)
        (d: SubModelData<'model, 'msg, 'bindingModel, 'bindingMsg, 'vm>) = {
      GetModel = d.GetModel >> ValueOption.map outMapBindingModel
      CreateViewModel = fun args -> d.CreateViewModel(args |> ViewModelArgs.map inMapBindingModel outMapBindingMsg)
      UpdateViewModel = fun (vm, m) -> (vm, inMapBindingModel m) |> d.UpdateViewModel
      ToMsg = fun m bMsg -> d.ToMsg m (inMapBindingMsg bMsg)
    }

    let boxMinorTypes d = d |> mapMinorTypes box box unbox unbox

    let create createViewModel updateViewModel =
      { GetModel = id
        CreateViewModel = createViewModel
        UpdateViewModel = updateViewModel
        ToMsg = Func2.id2 }
      |> boxMinorTypes
      |> SubModelData
      |> BaseBindingData

    let private mapFunctions
        mGetModel
        mGetBindings
        mToMsg
        (d: SubModelData<'model, 'msg, 'bindingModel, 'bindingMsg, 'vm>)
        : SubModelData<'model,'msg,'bindingModel,'bindingMsg,'vm> =
      { d with GetModel = mGetModel d.GetModel
               CreateViewModel = mGetBindings d.CreateViewModel
               ToMsg = mToMsg d.ToMsg }

    let measureFunctions
        mGetModel
        mGetBindings
        mToMsg =
      mapFunctions
        (mGetModel "getSubModel") // sic: "getModel" would be following the pattern
        (mGetBindings "bindings") // sic: "getBindings" would be following the pattern
        (mToMsg "toMsg")


  module SubModelWin =

    let mapMinorTypes
        (outMapBindingModel: 'bindingModel -> 'bindingModel0)
        (outMapBindingMsg: 'bindingMsg -> 'bindingMsg0)
        (inMapBindingModel: 'bindingModel0 -> 'bindingModel)
        (inMapBindingMsg: 'bindingMsg0 -> 'bindingMsg)
        (d: SubModelWinData<'model, 'msg, 'bindingModel, 'bindingMsg, 'vm>) = {
      GetState = d.GetState >> WindowState.map outMapBindingModel
      CreateViewModel = fun args -> d.CreateViewModel(args |> ViewModelArgs.map inMapBindingModel outMapBindingMsg)
      UpdateViewModel = fun (vm, m) -> d.UpdateViewModel (vm, inMapBindingModel m)
      ToMsg = fun m bMsg -> d.ToMsg m (inMapBindingMsg bMsg)
      GetWindow = d.GetWindow
      IsModal = d.IsModal
      OnCloseRequested = d.OnCloseRequested
    }

    let boxMinorTypes d = d |> mapMinorTypes box box unbox unbox

    let create getState createViewModel updateViewModel toMsg getWindow isModal onCloseRequested =
      { GetState = getState
        CreateViewModel = createViewModel
        UpdateViewModel = updateViewModel
        ToMsg = toMsg
        GetWindow = getWindow
        IsModal = isModal
        OnCloseRequested = onCloseRequested }
      |> boxMinorTypes
      |> SubModelWinData
      |> BaseBindingData

    let private mapFunctions
        mGetState
        mGetBindings
        mToMsg
        mGetWindow
        mOnCloseRequested
        (d: SubModelWinData<'model, 'msg, 'bindingModel, 'bindingMsg, 'vm>) =
      { d with GetState = mGetState d.GetState
               CreateViewModel = mGetBindings d.CreateViewModel
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


  module SubModelSeqUnkeyed =

    let mapMinorTypes
        (outMapBindingModel: 'bindingModel -> 'bindingModel0)
        (outMapBindingMsg: 'bindingMsg -> 'bindingMsg0)
        (outMapBindingViewModel: 'vm -> 'vm0)
        (inMapBindingModel: 'bindingModel0 -> 'bindingModel)
        (inMapBindingMsg: 'bindingMsg0 -> 'bindingMsg)
        (inMapBindingViewModel: 'vm0 -> 'vm)
        (d: SubModelSeqUnkeyedData<'model, 'msg, 'bindingModel, 'bindingMsg, 'vm, 'vmCollection>) = {
      GetModels = d.GetModels >> Seq.map outMapBindingModel
      CreateViewModel = fun args -> d.CreateViewModel(args |> ViewModelArgs.map inMapBindingModel outMapBindingMsg) |> outMapBindingViewModel
      CreateCollection = Seq.map inMapBindingViewModel >> d.CreateCollection >> CollectionTarget.mapA outMapBindingViewModel inMapBindingViewModel
      UpdateViewModel = fun (vm, m) -> d.UpdateViewModel (inMapBindingViewModel vm, inMapBindingModel m)
      ToMsg = fun m (idx, bMsg) -> d.ToMsg m (idx, (inMapBindingMsg bMsg))
    }

    let boxMinorTypes d = d |> mapMinorTypes box box box unbox unbox unbox

    let create createViewModel updateViewModel =
      { GetModels = (fun x -> upcast x)
        CreateViewModel = createViewModel
        CreateCollection = ObservableCollection >> CollectionTarget.create
        UpdateViewModel = updateViewModel
        ToMsg = Func2.id2 }
      |> boxMinorTypes
      |> SubModelSeqUnkeyedData
      |> BaseBindingData

    let private mapFunctions
        mGetModels
        mGetBindings
        mToMsg
        (d: SubModelSeqUnkeyedData<'model, 'msg, 'bindingModel, 'bindingMsg, 'vm, 'vmCollection>) =
      { d with GetModels = mGetModels d.GetModels
               CreateViewModel = mGetBindings d.CreateViewModel
               ToMsg = mToMsg d.ToMsg }

    let measureFunctions
        mGetModels
        mGetBindings
        mToMsg =
      mapFunctions
        (mGetModels "getSubModels") // sic: "getModels" would follow the pattern
        (mGetBindings "bindings") // sic: "getBindings" would follow the pattern
        (mToMsg "toMsg")


  module SubModelSeqKeyed =

      let mapMinorTypes
          (outMapBindingModel: 'bindingModel -> 'bindingModel0)
          (outMapBindingMsg: 'bindingMsg -> 'bindingMsg0)
          (outMapBindingViewModel: 'vm -> 'vm0)
          (outMapId: 'id -> 'id0)
          (inMapBindingModel: 'bindingModel0 -> 'bindingModel)
          (inMapBindingMsg: 'bindingMsg0 -> 'bindingMsg)
          (inMapBindingViewModel: 'vm0 -> 'vm)
          (inMapId: 'id0 -> 'id)
          (d: SubModelSeqKeyedData<'model, 'msg, 'bindingModel, 'bindingMsg, 'vm, 'vmCollection, 'id>) = {
        GetSubModels = d.GetSubModels >> Seq.map outMapBindingModel
        CreateViewModel = fun args -> d.CreateViewModel(args |> ViewModelArgs.map inMapBindingModel outMapBindingMsg) |> outMapBindingViewModel
        CreateCollection = Seq.map inMapBindingViewModel >> d.CreateCollection >> CollectionTarget.mapA outMapBindingViewModel inMapBindingViewModel
        UpdateViewModel = fun (vm, m) -> (inMapBindingViewModel vm, inMapBindingModel m) |> d.UpdateViewModel
        ToMsg = fun m (id, bMsg) -> d.ToMsg m ((inMapId id), (inMapBindingMsg bMsg))
        BmToId = inMapBindingModel >> d.BmToId >> outMapId
        VmToId = fun vm -> vm |> inMapBindingViewModel |> d.VmToId |> outMapId
      }

      let boxMinorTypes d = d |> mapMinorTypes box box box box unbox unbox unbox unbox

      let create createViewModel updateViewModel bmToId vmToId =
        { GetSubModels = (fun x -> upcast x)
          CreateViewModel = createViewModel
          CreateCollection = ObservableCollection >> CollectionTarget.create
          UpdateViewModel = updateViewModel
          ToMsg = Func2.id2
          BmToId = bmToId
          VmToId = vmToId }
        |> boxMinorTypes
        |> SubModelSeqKeyedData
        |> BaseBindingData

      let private mapFunctions
          mGetSubModels
          mGetBindings
          mToMsg
          mGetId
          (d: SubModelSeqKeyedData<'model, 'msg, 'bindingModel, 'bindingMsg, 'vm, 'vmCollection, 'id>) =
        { d with GetSubModels = mGetSubModels d.GetSubModels
                 CreateViewModel = mGetBindings d.CreateViewModel
                 ToMsg = mToMsg d.ToMsg
                 BmToId = mGetId d.BmToId }

      let measureFunctions
          mGetSubModels
          mGetBindings
          mToMsg
          mGetId =
        mapFunctions
          (mGetSubModels "getSubModels")
          (mGetBindings "getBindings")
          (mToMsg "toMsg")
          (mGetId "getId")


  module Validation =

    let private mapFunctions
        mValidate
        (d: ValidationData<'model, 'msg, 't>) =
      { d with Validate = mValidate d.Validate }

    let measureFunctions
        mValidate =
      mapFunctions
        (mValidate "validate")

  module Lazy =

    let private mapFunctions
        mGet
        mSet
        mEquals
        (d: LazyData<'model, 'msg, 'bindingModel, 'bindingMsg, 't>) =
      { d with Get = mGet d.Get
               Set = mSet d.Set
               Equals = mEquals d.Equals }

    let measureFunctions
        mGet
        mSet
        mEquals =
      mapFunctions
        (mGet "get")
        (mSet "set")
        (mEquals "equals")
