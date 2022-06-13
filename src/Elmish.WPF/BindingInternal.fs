namespace Elmish.WPF

open System.Windows

open Elmish


module internal Helper =

  let mapDispatch
      (getCurrentModel: unit -> 'model)
      (set: 'bindingMsg -> 'model -> 'msg)
      (dispatch: 'msg -> unit)
      : 'bindingMsg -> unit =
    fun bMsg -> getCurrentModel () |> set bMsg |> dispatch


type internal OneWayData<'model, 'a> =
  { Get: 'model -> 'a }


type internal OneWayToSourceData<'model, 'msg, 'a> =
  { Set: 'a -> 'model -> 'msg }


type internal OneWaySeqData<'model, 'a, 'id when 'id : equality> =
  { Get: 'model -> 'a seq
    CreateCollection: 'a seq -> CollectionTarget<'a>
    GetId: 'a -> 'id
    ItemEquals: 'a -> 'a -> bool }

  member d.Merge(values: CollectionTarget<'a>, newModel: 'model) =
    let create v _ = v
    let update oldVal newVal oldIdx =
      if not (d.ItemEquals newVal oldVal) then
        values.SetAt (oldIdx, newVal)
    let newVals = newModel |> d.Get |> Seq.toArray
    Merge.keyed d.GetId d.GetId create update values newVals


type internal TwoWayData<'model, 'msg, 'a> =
  { Get: 'model -> 'a
    Set: 'a -> 'model -> 'msg }


type internal CmdData<'model, 'msg> = {
  Exec: obj -> 'model -> 'msg voption
  CanExec: obj -> 'model -> bool
  AutoRequery: bool
}


type internal SubModelSelectedItemData<'model, 'msg, 'id> =
  { Get: 'model -> 'id voption
    Set: 'id voption -> 'model -> 'msg
    SubModelSeqBindingName: string }


type internal SubModelData<'model, 'msg, 'bindingModel, 'bindingMsg, 'bindingViewModel> = {
  GetModel: 'model -> 'bindingModel voption
  CreateViewModel: ViewModelArgs<'bindingModel,'bindingMsg> -> 'bindingViewModel
  UpdateViewModel: 'bindingViewModel * 'bindingModel -> unit
  ToMsg: 'model -> 'bindingMsg -> 'msg
}


and internal SubModelWinData<'model, 'msg, 'bindingModel, 'bindingMsg, 'bindingViewModel> = {
  GetState: 'model -> WindowState<'bindingModel>
  CreateViewModel: ViewModelArgs<'bindingModel,'bindingMsg> -> 'bindingViewModel
  UpdateViewModel: 'bindingViewModel * 'bindingModel -> unit
  ToMsg: 'model -> 'bindingMsg -> 'msg
  GetWindow: 'model -> Dispatch<'msg> -> Window
  IsModal: bool
  OnCloseRequested: 'model -> 'msg voption
}


and internal SubModelSeqUnkeyedData<'model, 'msg, 'bindingModel, 'bindingMsg, 'bindingViewModel> =
  { GetModels: 'model -> 'bindingModel seq
    CreateViewModel: ViewModelArgs<'bindingModel,'bindingMsg> -> 'bindingViewModel
    CreateCollection: 'bindingViewModel seq -> CollectionTarget<'bindingViewModel>
    UpdateViewModel: 'bindingViewModel * 'bindingModel -> unit
    ToMsg: 'model -> int * 'bindingMsg -> 'msg }


and internal SubModelSeqKeyedData<'model, 'msg, 'bindingModel, 'bindingMsg, 'bindingViewModel, 'id when 'id : equality> =
  { GetSubModels: 'model -> 'bindingModel seq
    CreateViewModel: ViewModelArgs<'bindingModel,'bindingMsg> -> 'bindingViewModel
    CreateCollection: 'bindingViewModel seq -> CollectionTarget<'bindingViewModel>
    UpdateViewModel: 'bindingViewModel * 'bindingModel -> unit
    GetUnderlyingModel: 'bindingViewModel -> 'bindingModel
    ToMsg: 'model -> 'id * 'bindingMsg -> 'msg
    GetId: 'bindingModel -> 'id }

  member d.MergeKeyed
      (getTargetId: ('bindingModel -> 'id) -> 't -> 'id,
       create: 'bindingModel -> 'id -> 't,
       update: 't -> 'bindingModel -> unit,
       values: CollectionTarget<'t>,
       newSubModels: 'bindingModel []) =
    let update t bm _ = update t bm
    Merge.keyed d.GetId (getTargetId d.GetId) create update values newSubModels


and internal ValidationData<'model, 'msg, 'a> =
  { BindingData: BindingData<'model, 'msg, 'a>
    Validate: 'model -> string list }


and internal LazyData<'model, 'msg, 'bindingModel, 'bindingMsg, 'a> =
  { BindingData: BindingData<'bindingModel, 'bindingMsg, 'a>
    Get: 'model -> 'bindingModel
    Set: 'bindingMsg -> 'model -> 'msg
    Equals: 'bindingModel -> 'bindingModel -> bool }

  member this.MapDispatch
      (getCurrentModel: unit -> 'model,
       dispatch: 'msg -> unit)
       : 'bindingMsg -> unit =
    Helper.mapDispatch getCurrentModel this.Set dispatch


and internal AlterMsgStreamData<'model, 'msg, 'bindingModel, 'bindingMsg, 'dispatchMsg, 'a> =
 { BindingData: BindingData<'bindingModel, 'bindingMsg, 'a>
   Get: 'model -> 'bindingModel
   Set: 'dispatchMsg -> 'model -> 'msg
   AlterMsgStream: ('dispatchMsg -> unit) -> 'bindingMsg -> unit }

  member this.MapDispatch
      (getCurrentModel: unit -> 'model,
       dispatch: 'msg -> unit)
       : 'bindingMsg -> unit =
    Helper.mapDispatch getCurrentModel this.Set dispatch
    |> this.AlterMsgStream


and internal BaseBindingData<'model, 'msg, 'a> =
  | OneWayData of OneWayData<'model, 'a>
  | OneWayToSourceData of OneWayToSourceData<'model, 'msg, 'a>
  | OneWaySeqData of OneWaySeqData<'model, obj, obj>
  | TwoWayData of TwoWayData<'model, 'msg, 'a>
  | CmdData of CmdData<'model, 'msg>
  | SubModelData of SubModelData<'model, 'msg, obj, obj, 'a>
  | SubModelWinData of SubModelWinData<'model, 'msg, obj, obj, 'a>
  | SubModelSeqUnkeyedData of SubModelSeqUnkeyedData<'model, 'msg, obj, obj, obj>
  | SubModelSeqKeyedData of SubModelSeqKeyedData<'model, 'msg, obj, obj, obj, obj>
  | SubModelSelectedItemData of SubModelSelectedItemData<'model, 'msg, obj>


and internal BindingData<'model, 'msg, 'a> =
  | BaseBindingData of BaseBindingData<'model, 'msg, 'a>
  | CachingData of BindingData<'model, 'msg, 'a>
  | ValidationData of ValidationData<'model, 'msg, 'a>
  | LazyData of LazyData<'model, 'msg, obj, obj, 'a>
  | AlterMsgStreamData of AlterMsgStreamData<'model, 'msg, obj, obj, obj, 'a>


/// Represents all necessary data used to create a binding.
and Binding<'model, 'msg> =
  internal
    { Name: string
      Data: BindingData<'model, 'msg, obj> }


[<AutoOpen>]
module internal Helpers =

  let createBinding data name =
    { Name = name
      Data = data }


module internal BindingData =

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
          GetUnderlyingModel = d.GetUnderlyingModel
          ToMsg = f >> d.ToMsg
          GetId = d.GetId
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
          GetUnderlyingModel = d.GetUnderlyingModel
          ToMsg = fun m bMsg -> f (d.ToMsg m bMsg) m
          GetId = d.GetId
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
      (b: BindingData<'bindingModel, 'bindingMsg, 'a>)
      : BindingData<'model, 'msg, 'a> =
    { BindingData = b |> mapModel unbox |> mapMsg box
      Get = box
      Set = fun (dMsg: obj) _ -> unbox dMsg
      AlterMsgStream =
        fun (f: obj -> unit) ->
          let f' = box >> f
          let g = alteration f'
          unbox >> g
    } |> AlterMsgStreamData
  let addSticky (predicate: 'model -> bool) (binding: BindingData<'model, 'msg, 'a>) =
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

    let mapFunctions
        mGet
        (d: OneWayData<'model, 'a>) =
      { d with Get = mGet d.Get }

    let measureFunctions
        mGet =
      mapFunctions
        (mGet "get")


  module OneWayToSource =

    let mapFunctions
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
        (d: OneWaySeqData<'model, 'a, 'id>) = {
      Get = d.Get >> Seq.map outMapA
      CreateCollection = Seq.map inMapA >> d.CreateCollection >> CollectionTarget.map outMapA inMapA
      GetId = inMapA >> d.GetId >> outMapId
      ItemEquals = fun a1 a2 -> d.ItemEquals (inMapA a1) (inMapA a2)
    }

    let box d = mapMinorTypes box box unbox d

    let mapFunctions
        mGet
        mGetId
        mItemEquals
        (d: OneWaySeqData<'model, 'a, 'id>) =
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


  module Cmd =

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


  module SubModelSelectedItem =

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


  module SubModel =

    let mapMinorTypes
        (outMapBindingModel: 'bindingModel -> 'bindingModel0)
        (outMapBindingMsg: 'bindingMsg -> 'bindingMsg0)
        (outMapBindingViewModel: 'bindingViewModel -> 'bindingViewModel0)
        (inMapBindingModel: 'bindingModel0 -> 'bindingModel)
        (inMapBindingMsg: 'bindingMsg0 -> 'bindingMsg)
        (inMapBindingViewModel: 'bindingViewModel0 -> 'bindingViewModel)
        (d: SubModelData<'model, 'msg, 'bindingModel, 'bindingMsg, 'bindingViewModel>) = {
      GetModel = d.GetModel >> ValueOption.map outMapBindingModel
      CreateViewModel = fun args -> d.CreateViewModel(args |> ViewModelArgs.map inMapBindingModel outMapBindingMsg) |> outMapBindingViewModel
      UpdateViewModel = fun (vm,m) -> (inMapBindingViewModel vm, inMapBindingModel m) |> d.UpdateViewModel
      ToMsg = fun m bMsg -> d.ToMsg m (inMapBindingMsg bMsg)
    }
    
    let boxMinorTypes d = mapMinorTypes box box box unbox unbox unbox d

    let mapFunctions
        mGetModel
        mGetBindings
        mToMsg
        (d: SubModelData<'model, 'msg, 'bindingModel, 'bindingMsg, 'bindingViewModel>) =
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
        (outMapBindingViewModel: 'bindingViewModel -> 'bindingViewModel0)
        (inMapBindingModel: 'bindingModel0 -> 'bindingModel)
        (inMapBindingMsg: 'bindingMsg0 -> 'bindingMsg)
        (inMapBindingViewModel: 'bindingViewModel0 -> 'bindingViewModel)
        (d: SubModelWinData<'model, 'msg, 'bindingModel, 'bindingMsg, 'bindingViewModel>) = {
      GetState = d.GetState >> WindowState.map outMapBindingModel
      CreateViewModel = fun args -> d.CreateViewModel(args |> ViewModelArgs.map inMapBindingModel outMapBindingMsg) |> outMapBindingViewModel
      UpdateViewModel = fun (vm,m) -> d.UpdateViewModel (inMapBindingViewModel vm,inMapBindingModel m)
      ToMsg = fun m bMsg -> d.ToMsg m (inMapBindingMsg bMsg)
      GetWindow = d.GetWindow
      IsModal = d.IsModal
      OnCloseRequested = d.OnCloseRequested
    }

    let box d = mapMinorTypes box box box unbox unbox unbox d

    let mapFunctions
        mGetState
        mGetBindings
        mToMsg
        mGetWindow
        mOnCloseRequested
        (d: SubModelWinData<'model, 'msg, 'bindingModel, 'bindingMsg, 'bindingViewModel>) =
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
        (outMapBindingViewModel: 'bindingViewModel -> 'bindingViewModel0)
        (inMapBindingModel: 'bindingModel0 -> 'bindingModel)
        (inMapBindingMsg: 'bindingMsg0 -> 'bindingMsg)
        (inMapBindingViewModel: 'bindingViewModel0 -> 'bindingViewModel)
        (d: SubModelSeqUnkeyedData<'model, 'msg, 'bindingModel, 'bindingMsg, 'bindingViewModel>) = {
      GetModels = d.GetModels >> Seq.map outMapBindingModel
      CreateViewModel = fun args -> d.CreateViewModel(args |> ViewModelArgs.map inMapBindingModel outMapBindingMsg) |> outMapBindingViewModel
      CreateCollection = Seq.map inMapBindingViewModel >> d.CreateCollection >> CollectionTarget.map outMapBindingViewModel inMapBindingViewModel
      UpdateViewModel = fun (vm,m) -> d.UpdateViewModel (inMapBindingViewModel vm,inMapBindingModel m)
      ToMsg = fun m (idx, bMsg) -> d.ToMsg m (idx, (inMapBindingMsg bMsg))
    }

    let box d = mapMinorTypes box box box unbox unbox unbox d

    let mapFunctions
        mGetModels
        mGetBindings
        mToMsg
        (d: SubModelSeqUnkeyedData<'model, 'msg, 'bindingModel, 'bindingMsg, 'bindingViewModel>) =
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
          (outMapBindingViewModel: 'bindingViewModel -> 'bindingViewModel0)
          (outMapId: 'id -> 'id0)
          (inMapBindingModel: 'bindingModel0 -> 'bindingModel)
          (inMapBindingMsg: 'bindingMsg0 -> 'bindingMsg)
          (inMapBindingViewModel: 'bindingViewModel0 -> 'bindingViewModel)
          (inMapId: 'id0 -> 'id)
          (d: SubModelSeqKeyedData<'model, 'msg, 'bindingModel, 'bindingMsg, 'bindingViewModel, 'id>) = {
        GetSubModels = d.GetSubModels >> Seq.map outMapBindingModel
        CreateViewModel = fun args -> d.CreateViewModel(args |> ViewModelArgs.map inMapBindingModel outMapBindingMsg) |> outMapBindingViewModel
        CreateCollection = Seq.map inMapBindingViewModel >> d.CreateCollection >> CollectionTarget.map outMapBindingViewModel inMapBindingViewModel
        UpdateViewModel = fun (vm,m) -> (inMapBindingViewModel vm,inMapBindingModel m) |> d.UpdateViewModel
        GetUnderlyingModel = fun vm -> vm |> inMapBindingViewModel |> d.GetUnderlyingModel |> outMapBindingModel
        ToMsg = fun m (id, bMsg) -> d.ToMsg m ((inMapId id), (inMapBindingMsg bMsg))
        GetId = inMapBindingModel >> d.GetId >> outMapId
      }

      let box d = mapMinorTypes box box box box unbox unbox unbox unbox d

      let mapFunctions
          mGetSubModels
          mGetBindings
          mToMsg
          mGetId
          (d: SubModelSeqKeyedData<'model, 'msg, 'bindingModel, 'bindingMsg, 'bindingViewModel, 'id>) =
        { d with GetSubModels = mGetSubModels d.GetSubModels
                 CreateViewModel = mGetBindings d.CreateViewModel
                 ToMsg = mToMsg d.ToMsg
                 GetId = mGetId d.GetId }

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

    let mapFunctions
        mValidate
        (d: ValidationData<'model, 'msg, 'a>) =
      { d with Validate = mValidate d.Validate }

    let measureFunctions
        mValidate =
      mapFunctions
        (mValidate "validate")

  module Lazy =

    let mapFunctions
        mGet
        mSet
        mEquals
        (d: LazyData<'model, 'msg, 'bindingModel, 'bindingMsg, 'a>) =
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
