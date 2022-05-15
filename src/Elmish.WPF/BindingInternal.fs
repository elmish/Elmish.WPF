namespace Elmish.WPF

open System.Collections.ObjectModel
open System.Windows

open Elmish


type internal OneWayData<'model, 'a> =
  { Get: 'model -> 'a }


type internal OneWayToSourceData<'model, 'msg, 'a> =
  { Set: 'a -> 'model -> 'msg }


type internal OneWaySeqLazyData<'model, 'a, 'b, 'id when 'id : equality> =
  { Get: 'model -> 'a
    Map: 'a -> 'b seq
    CreateCollection: 'b seq -> CollectionTarget<'b>
    Equals: 'a -> 'a -> bool
    GetId: 'b -> 'id
    ItemEquals: 'b -> 'b -> bool }

  member d.Merge(values: CollectionTarget<'b>, currentModel: 'model, newModel: 'model) =
    let intermediate = d.Get newModel
    if not <| d.Equals intermediate (d.Get currentModel) then
      let create v _ = v
      let update oldVal newVal oldIdx =
        if not (d.ItemEquals newVal oldVal) then
          values.SetAt (oldIdx, newVal)
      let newVals = intermediate |> d.Map |> Seq.toArray
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


and internal ValidationData<'model, 'msg> =
  { BindingData: BindingData<'model, 'msg>
    Validate: 'model -> string list }


and internal LazyData<'model, 'msg> =
  { BindingData: BindingData<'model, 'msg>
    Equals: 'model -> 'model -> bool }


and internal AlterMsgStreamData<'model, 'msg, 'bindingModel, 'bindingMsg, 'dispatchMsg> =
 { BindingData: BindingData<'bindingModel, 'bindingMsg>
   AlterMsgStream: ('dispatchMsg -> unit) -> 'bindingMsg -> unit
   Get: 'model -> 'bindingModel
   Set: 'dispatchMsg -> 'model -> 'msg }

  member this.CreateFinalDispatch
      (getCurrentModel: unit -> 'model,
       dispatch: 'msg -> unit)
       : 'bindingMsg -> unit =
    let dispatch' (dMsg: 'dispatchMsg) = getCurrentModel () |> this.Set dMsg |> dispatch
    this.AlterMsgStream dispatch'


and internal BaseBindingData<'model, 'msg> =
  | OneWayData of OneWayData<'model, obj>
  | OneWayToSourceData of OneWayToSourceData<'model, 'msg, obj>
  | OneWaySeqLazyData of OneWaySeqLazyData<'model, obj, obj, obj>
  | TwoWayData of TwoWayData<'model, 'msg, obj>
  | CmdData of CmdData<'model, 'msg>
  | SubModelData of SubModelData<'model, 'msg, obj, obj, obj>
  | SubModelWinData of SubModelWinData<'model, 'msg, obj, obj, obj>
  | SubModelSeqUnkeyedData of SubModelSeqUnkeyedData<'model, 'msg, obj, obj, obj>
  | SubModelSeqKeyedData of SubModelSeqKeyedData<'model, 'msg, obj, obj, obj, obj>
  | SubModelSelectedItemData of SubModelSelectedItemData<'model, 'msg, obj>


and internal BindingData<'model, 'msg> =
  | BaseBindingData of BaseBindingData<'model, 'msg>
  | CachingData of BindingData<'model, 'msg>
  | ValidationData of ValidationData<'model, 'msg>
  | LazyData of LazyData<'model, 'msg>
  | AlterMsgStreamData of AlterMsgStreamData<'model, 'msg, obj, obj, obj>


/// Represents all necessary data used to create a binding.
and Binding<'model, 'msg> =
  internal
    { Name: string
      Data: BindingData<'model, 'msg> }


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
      | OneWaySeqLazyData d -> OneWaySeqLazyData {
          Get = f >> d.Get
          Map = d.Map
          CreateCollection = d.CreateCollection
          Equals = d.Equals
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
          BindingData = recursiveCase d.BindingData
          Equals = fun a1 a2 -> d.Equals (f a1) (f a2)
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
      | OneWaySeqLazyData d -> d |> OneWaySeqLazyData
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
      | LazyData d -> LazyData {
          BindingData = recursiveCase d.BindingData
          Equals = d.Equals
        }
      | AlterMsgStreamData d -> AlterMsgStreamData {
          BindingData = d.BindingData
          AlterMsgStream = d.AlterMsgStream
          Get = d.Get
          Set = fun bMsg m -> f (d.Set bMsg m) m
        }
    recursiveCase

  let mapMsg f = mapMsgWithModel (fun a _ -> f a)

  let setMsgWithModel f = mapMsgWithModel (fun _ m -> f m)
  let setMsg msg = mapMsg (fun _ -> msg)

  let addCaching b = b |> CachingData
  let addValidation validate b = { BindingData = b; Validate = validate } |> ValidationData
  let addLazy equals b = { BindingData = b; Equals = equals } |> LazyData
  let alterMsgStream
      (alteration: ('dispatchMsg -> unit) -> 'bindingMsg -> unit)
      (b: BindingData<'bindingModel, 'bindingMsg>)
      : BindingData<'model, 'msg> =
    { BindingData = b |> mapModel unbox |> mapMsg box
      AlterMsgStream =
        fun (f: obj -> unit) ->
          let f' = box >> f
          let g = alteration f'
          unbox >> g
      Get = box
      Set = fun (dMsg: obj) _ -> unbox dMsg }
    |> AlterMsgStreamData
  let addSticky (predicate: 'model -> bool) (binding: BindingData<'model, 'msg>) =
    let mutable stickyModel = None
    let f newModel =
      if predicate newModel then
        stickyModel <- Some newModel
        newModel
      else
        stickyModel |> Option.defaultValue newModel
    binding |> mapModel f


  module Binding =

    let mapData f binding =
      { Name = binding.Name
        Data = binding.Data |> f }

    let mapModel f = f |> mapModel |> mapData
    let mapMsgWithModel f = f |> mapMsgWithModel |> mapData
    let mapMsg f = f |> mapMsg |> mapData

    let setMsgWithModel f = f |> setMsgWithModel |> mapData
    let setMsg msg = msg |> setMsg |> mapData

    let addCaching<'model, 'msg> : Binding<'model, 'msg> -> Binding<'model, 'msg> = addCaching |> mapData
    let addValidation vaidate = vaidate |> addValidation |> mapData
    let addLazy equals = equals |> addLazy |> mapData
    let addSticky predicate =  predicate |> addSticky |> mapData
    let alterMsgStream alteration = alteration |> alterMsgStream |> mapData


  module Bindings =

    let mapModel f = f |> Binding.mapModel |> List.map
    let mapMsgWithModel f = f |> Binding.mapMsgWithModel |> List.map
    let mapMsg f = f |> Binding.mapMsg |> List.map


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


  module OneWaySeqLazy =

    let mapMinorTypes
        (outMapA: 'a -> 'a0)
        (outMapB: 'b -> 'b0)
        (outMapId: 'id -> 'id0)
        (inMapA: 'a0 -> 'a)
        (inMapB: 'b0 -> 'b)
        (d: OneWaySeqLazyData<'model, 'a, 'b, 'id>) = {
      Get = d.Get >> outMapA
      Map = inMapA >> d.Map >> Seq.map outMapB
      CreateCollection = Seq.map inMapB >> d.CreateCollection >> CollectionTarget.map outMapB inMapB
      Equals = fun a1 a2 -> d.Equals (inMapA a1) (inMapA a2)
      GetId = inMapB >> d.GetId >> outMapId
      ItemEquals = fun b1 b2 -> d.ItemEquals (inMapB b1) (inMapB b2)
    }

    let box d = mapMinorTypes box box box unbox unbox d

    let create get equals map itemEquals getId =
      { Get = get
        Equals = equals
        Map = fun a -> upcast map a
        CreateCollection = ObservableCollection >> CollectionTarget.create
        ItemEquals = itemEquals
        GetId = getId }
      |> box
      |> OneWaySeqLazyData
      |> BaseBindingData
      |> createBinding

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

    let createWithParam exec canExec autoRequery =
      { Exec = exec
        CanExec = canExec
        AutoRequery = autoRequery }
      |> CmdData
      |> BaseBindingData
      |> createBinding

    let create exec canExec =
      createWithParam
        (fun _ -> exec)
        (fun _ -> canExec)
        false
      >> Binding.addLazy (fun m1 m2 -> canExec m1 = canExec m2)

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

    let create getState createViewModel updateViewModel toMsg getWindow isModal onCloseRequested =
      { GetState = getState
        CreateViewModel = createViewModel
        UpdateViewModel = updateViewModel
        ToMsg = toMsg
        GetWindow = getWindow
        IsModal = isModal
        OnCloseRequested = onCloseRequested }
      |> box
      |> SubModelWinData
      |> BaseBindingData
      |> createBinding

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

    let create createViewModel updateViewModel =
      { GetModels = id
        CreateViewModel = createViewModel
        CreateCollection = ObservableCollection >> CollectionTarget.create
        UpdateViewModel = updateViewModel
        ToMsg = fun _ -> id }
      |> box
      |> SubModelSeqUnkeyedData
      |> BaseBindingData
      |> createBinding

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

      let create createViewModel updateViewModel getUnderlyingModel getId =
        { GetSubModels = id
          CreateViewModel = createViewModel
          CreateCollection = ObservableCollection >> CollectionTarget.create
          UpdateViewModel = updateViewModel
          GetUnderlyingModel = getUnderlyingModel
          ToMsg = fun _ -> id
          GetId = getId }
        |> box
        |> SubModelSeqKeyedData
        |> BaseBindingData
        |> createBinding

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
        (d: ValidationData<'model, 'msg>) =
      { d with Validate = mValidate d.Validate }

    let measureFunctions
        mValidate =
      mapFunctions
        (mValidate "validate")

  module Lazy =

    let mapFunctions
        mEquals
        (d: LazyData<'model, 'msg>) =
      { d with Equals = mEquals d.Equals }

    let measureFunctions
        mEquals =
      mapFunctions
        (mEquals "equals")
