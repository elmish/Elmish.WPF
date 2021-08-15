namespace Elmish.WPF

open System.Collections.ObjectModel
open System.Windows

open Elmish



type internal OneWayData<'model> =
  { Get: 'model -> obj }


type internal OneWayToSourceData<'model, 'msg> =
  { Set: obj -> 'model -> 'msg }


type internal OneWaySeqLazyData<'model, 'a, 'b, 'id when 'id : equality> =
  { Get: 'model -> 'a
    Map: 'a -> 'b seq
    Equals: 'a -> 'a -> bool
    GetId: 'b -> 'id
    ItemEquals: 'b -> 'b -> bool }

  member d.Merge(values: ObservableCollection<'b>, currentModel: 'model, newModel: 'model) =
    let intermediate = d.Get newModel
    if not <| d.Equals intermediate (d.Get currentModel) then
      let create v _ = v
      let update oldVal newVal oldIdx =
        if not (d.ItemEquals newVal oldVal) then
          values.[oldIdx] <- newVal
      let newVals = intermediate |> d.Map |> Seq.toArray
      Merge.keyed d.GetId d.GetId create update values newVals


type internal TwoWayData<'model, 'msg> =
  { Get: 'model -> obj
    Set: obj -> 'model -> 'msg }


type internal CmdData<'model, 'msg> = {
  Exec: obj -> 'model -> 'msg voption
  CanExec: obj -> 'model -> bool
  AutoRequery: bool
}


type internal SubModelSelectedItemData<'model, 'msg, 'id when 'id : equality> =
  { Get: 'model -> 'id voption
    Set: 'id voption -> 'model -> 'msg
    SubModelSeqBindingName: string }


and internal SubModelData<'model, 'msg, 'bindingModel, 'bindingMsg> = {
  GetModel: 'model -> 'bindingModel voption
  GetBindings: unit -> Binding<'bindingModel, 'bindingMsg> list
  ToMsg: 'model -> 'bindingMsg -> 'msg
}


and internal SubModelWinData<'model, 'msg, 'bindingModel, 'bindingMsg> = {
  GetState: 'model -> WindowState<'bindingModel>
  GetBindings: unit -> Binding<'bindingModel, 'bindingMsg> list
  ToMsg: 'model -> 'bindingMsg -> 'msg
  GetWindow: 'model -> Dispatch<'msg> -> Window
  IsModal: bool
  OnCloseRequested: 'model -> 'msg voption
}


and internal SubModelSeqUnkeyedData<'model, 'msg, 'bindingModel, 'bindingMsg> =
  { GetModels: 'model -> 'bindingModel seq
    GetBindings: unit -> Binding<'bindingModel, 'bindingMsg> list
    ToMsg: 'model -> int * 'bindingMsg -> 'msg }


and internal SubModelSeqKeyedData<'model, 'msg, 'bindingModel, 'bindingMsg, 'id when 'id : equality> =
  { GetSubModels: 'model -> 'bindingModel seq
    GetBindings: unit -> Binding<'bindingModel, 'bindingMsg> list
    ToMsg: 'model -> 'id * 'bindingMsg -> 'msg
    GetId: 'bindingModel -> 'id }

  member d.MergeKeyed
      (getTargetId: ('bindingModel -> 'id) -> 't -> 'id,
       create: 'bindingModel -> 'id -> 't,
       update: 't -> 'bindingModel -> unit,
       values: ObservableCollection<'t>,
       newSubModels: 'bindingModel []) =
    let update t bm _ = update t bm
    Merge.keyed d.GetId (getTargetId d.GetId) create update values newSubModels


and internal ValidationData<'model, 'msg> =
  { BindingData: BindingData<'model, 'msg>
    Validate: 'model -> string list }


and internal LazyData<'model, 'msg> =
  { BindingData: BindingData<'model, 'msg>
    Equals: 'model -> 'model -> bool }


and internal WrapDispatchData<'model, 'msg> =
 { BindingData: BindingData<'model, 'msg>
   WrapDispatch: (obj -> unit) -> obj -> unit }


/// Represents all necessary data used to create the different binding types.
and internal BaseBindingData<'model, 'msg> =
  | OneWayData of OneWayData<'model>
  | OneWayToSourceData of OneWayToSourceData<'model, 'msg>
  | OneWaySeqLazyData of OneWaySeqLazyData<'model, obj, obj, obj>
  | TwoWayData of TwoWayData<'model, 'msg>
  | CmdData of CmdData<'model, 'msg>
  | SubModelData of SubModelData<'model, 'msg, obj, obj>
  | SubModelWinData of SubModelWinData<'model, 'msg, obj, obj>
  | SubModelSeqUnkeyedData of SubModelSeqUnkeyedData<'model, 'msg, obj, obj>
  | SubModelSeqKeyedData of SubModelSeqKeyedData<'model, 'msg, obj, obj, obj>
  | SubModelSelectedItemData of SubModelSelectedItemData<'model, 'msg, obj>


/// Represents all necessary data used to create the different binding types.
and internal BindingData<'model, 'msg> =
  | BaseBindingData of BaseBindingData<'model, 'msg>
  | CachingData of BindingData<'model, 'msg>
  | ValidationData of ValidationData<'model, 'msg>
  | LazyData of LazyData<'model, 'msg>
  | WrapDispatchData of WrapDispatchData<'model, 'msg>


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

  let rec getBaseBindingData = function
    | BaseBindingData d -> d
    | CachingData d -> getBaseBindingData d
    | ValidationData d -> getBaseBindingData d.BindingData
    | LazyData d -> getBaseBindingData d.BindingData
    | WrapDispatchData d -> getBaseBindingData d.BindingData

  let rec getFirstLazyData = function
    | BaseBindingData _ -> None
    | LazyData d -> Some d
    | CachingData d -> getFirstLazyData d
    | ValidationData d -> getFirstLazyData d.BindingData
    | WrapDispatchData d -> getFirstLazyData d.BindingData


module internal BindingData =

  let subModelSelectedItemLast a b =
    let getComparisonNumber =
      let baseCase = function
        | SubModelSelectedItemData _ -> 1
        | _ -> 0
      let rec recrusiveCase = function
        | BaseBindingData d -> baseCase d
        | CachingData d -> recrusiveCase d
        | ValidationData d -> recrusiveCase d.BindingData
        | LazyData d -> recrusiveCase d.BindingData
        | WrapDispatchData d -> recrusiveCase d.BindingData
      recrusiveCase
    (getComparisonNumber a) - (getComparisonNumber b)

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
          GetBindings = d.GetBindings
          ToMsg = f >> d.ToMsg
        }
      | SubModelWinData d -> SubModelWinData {
          GetState = f >> d.GetState
          GetBindings = d.GetBindings
          ToMsg = f >> d.ToMsg
          GetWindow = f >> d.GetWindow
          IsModal = d.IsModal
          OnCloseRequested = f >> d.OnCloseRequested
        }
      | SubModelSeqUnkeyedData d -> SubModelSeqUnkeyedData {
          GetModels = f >> d.GetModels
          GetBindings = d.GetBindings
          ToMsg = f >> d.ToMsg
        }
      | SubModelSeqKeyedData d -> SubModelSeqKeyedData {
          GetSubModels = f >> d.GetSubModels
          GetBindings = d.GetBindings
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
      | WrapDispatchData d -> WrapDispatchData {
          BindingData = recursiveCase d.BindingData
          WrapDispatch = d.WrapDispatch
        }
    recursiveCase

  let mapMsgWithModel f =
    let baseCase = function
      | OneWayData d -> d |> OneWayData
      | OneWayToSourceData d -> OneWayToSourceData {
          Set = fun v m -> d.Set v m |> f m
        }
      | OneWaySeqLazyData d -> d |> OneWaySeqLazyData
      | TwoWayData d -> TwoWayData {
          Get = d.Get
          Set = fun v m -> d.Set v m |> f m
        }
      | CmdData d -> CmdData {
          Exec = fun p m -> d.Exec p m |> ValueOption.map (f m)
          CanExec = fun p m -> d.CanExec p m
          AutoRequery = d.AutoRequery
        }
      | SubModelData d -> SubModelData {
          GetModel = d.GetModel
          GetBindings = d.GetBindings
          ToMsg = fun m x -> (m, x) ||> d.ToMsg |> f m
        }
      | SubModelWinData d -> SubModelWinData {
          GetState = d.GetState
          GetBindings = d.GetBindings
          ToMsg = fun m x -> (m, x) ||> d.ToMsg |> f m
          GetWindow = fun m dispatch -> d.GetWindow m (m |> f >> dispatch)
          IsModal = d.IsModal
          OnCloseRequested = fun m -> m |> d.OnCloseRequested |> ValueOption.map (f m)
        }
      | SubModelSeqUnkeyedData d -> SubModelSeqUnkeyedData {
          GetModels = d.GetModels
          GetBindings = d.GetBindings
          ToMsg = fun m x -> (m, x) ||> d.ToMsg |> f m
        }
      | SubModelSeqKeyedData d -> SubModelSeqKeyedData {
          GetSubModels = d.GetSubModels
          GetBindings = d.GetBindings
          ToMsg = fun m x -> (m, x) ||> d.ToMsg |> f m
          GetId = d.GetId
        }
      | SubModelSelectedItemData d -> SubModelSelectedItemData {
          Get = d.Get
          Set = fun v m -> d.Set v m |> f m
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
      | WrapDispatchData d -> WrapDispatchData {
          BindingData = recursiveCase d.BindingData
          WrapDispatch = d.WrapDispatch
        }
    recursiveCase

  let mapMsg f = mapMsgWithModel (fun _ -> f)

  let setMsgWithModel f = mapMsgWithModel (fun m _ -> f m)
  let setMsg msg = mapMsg (fun _ -> msg)

  let addCaching b = b |> CachingData
  let addValidation validate b = { BindingData = b; Validate = validate } |> ValidationData
  let addLazy equals b = { BindingData = b; Equals = equals } |> LazyData
  let addWrapDispatch wrapDispatch b = { BindingData = b; WrapDispatch = wrapDispatch } |> WrapDispatchData
  let addSticky (predicate: 'model -> bool) (binding: BindingData<'model, 'msg>) =
    let mutable stickyModel = None
    let f newModel =
      match predicate newModel, stickyModel with
      | _, None ->
          newModel
      | true, _ ->
          stickyModel <- Some newModel
          newModel
      | false, Some sm ->
          sm
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
    let addWrapDispatch wrapDispatch = wrapDispatch |> addWrapDispatch |> mapData
    let addSticky predicate =  predicate |> addSticky |> mapData


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
        (d: OneWayData<'model>) =
      { d with Get = mGet d.Get }

    let measureFunctions
        mGet =
      mapFunctions
        (mGet "get")


  module OneWayToSource =

    let mapFunctions
        mSet
        (d: OneWayToSourceData<'model, 'msg>) =
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
      Equals = fun a1 a2 -> d.Equals (inMapA a1) (inMapA a2)
      GetId = inMapB >> d.GetId >> outMapId
      ItemEquals = fun b1 b2 -> d.ItemEquals (inMapB b1) (inMapB b2)
    }

    let box d = mapMinorTypes box box box unbox unbox d

    let create get equals map itemEquals getId =
      { Get = get
        Equals = equals
        Map = fun a -> upcast map a
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
        (d: TwoWayData<'model, 'msg>) =
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

    let mapMinorTypes
        (outMapId: 'id -> 'id0)
        (inMapId: 'id0 -> 'id)
        (d: SubModelSelectedItemData<'model, 'msg, 'id>) = {
      Get = d.Get >> ValueOption.map outMapId
      Set = ValueOption.map inMapId >> d.Set
      SubModelSeqBindingName = d.SubModelSeqBindingName
    }

    let box d = mapMinorTypes box unbox d

    let create get set subModelSeqBindingName =
      { Get = get
        Set = set
        SubModelSeqBindingName = subModelSeqBindingName }
      |> box
      |> SubModelSelectedItemData
      |> BaseBindingData
      |> createBinding

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


  module SubModelWin =

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

    let create getState bindings toMsg getWindow isModal onCloseRequested =
      { GetState = getState
        GetBindings = bindings
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


  module SubModelSeqUnkeyed =

    let mapMinorTypes
        (outMapBindingModel: 'bindingModel -> 'bindingModel0)
        (outMapBindingMsg: 'bindingMsg -> 'bindingMsg0)
        (inMapBindingModel: 'bindingModel0 -> 'bindingModel)
        (inMapBindingMsg: 'bindingMsg0 -> 'bindingMsg)
        (d: SubModelSeqUnkeyedData<'model, 'msg, 'bindingModel, 'bindingMsg>) = {
      GetModels = d.GetModels >> Seq.map outMapBindingModel
      GetBindings = d.GetBindings >> Bindings.mapModel inMapBindingModel >> Bindings.mapMsg outMapBindingMsg
      ToMsg = fun m (idx, bMsg) -> d.ToMsg m (idx, (inMapBindingMsg bMsg))
    }

    let box d = mapMinorTypes box box unbox unbox d

    let create getBindings =
      { GetModels = id
        GetBindings = getBindings
        ToMsg = fun _ -> id }
      |> box
      |> SubModelSeqUnkeyedData
      |> BaseBindingData
      |> createBinding

    let mapFunctions
        mGetModels
        mGetBindings
        mToMsg
        (d: SubModelSeqUnkeyedData<'model, 'msg, 'bindingModel, 'bindingMsg>) =
      { d with GetModels = mGetModels d.GetModels
               GetBindings = mGetBindings d.GetBindings
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
          (outMapId: 'id -> 'id0)
          (inMapBindingModel: 'bindingModel0 -> 'bindingModel)
          (inMapBindingMsg: 'bindingMsg0 -> 'bindingMsg)
          (inMapId: 'id0 -> 'id)
          (d: SubModelSeqKeyedData<'model, 'msg, 'bindingModel, 'bindingMsg, 'id>) = {
        GetSubModels = d.GetSubModels >> Seq.map outMapBindingModel
        GetBindings = d.GetBindings >> Bindings.mapModel inMapBindingModel >> Bindings.mapMsg outMapBindingMsg
        ToMsg = fun m (id, bMsg) -> d.ToMsg m ((inMapId id), (inMapBindingMsg bMsg))
        GetId = inMapBindingModel >> d.GetId >> outMapId
      }

      let box d = mapMinorTypes box box box unbox unbox unbox d

      let create getBindings getId =
        { GetSubModels = id
          GetBindings = getBindings
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
          (d: SubModelSeqKeyedData<'model, 'msg, 'bindingModel, 'bindingMsg, 'id>) =
        { d with GetSubModels = mGetSubModels d.GetSubModels
                 GetBindings = mGetBindings d.GetBindings
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



module Bindings =

  /// Map the model of a list of bindings via a contravariant mapping.
  let mapModel (f: 'a -> 'b) (bindings: Binding<'b, 'msg> list) = BindingData.Bindings.mapModel f bindings

  /// Map the message of a list of bindings with access to the model via a covariant mapping.
  let mapMsgWithModel (f: 'model -> 'a -> 'b) (bindings: Binding<'model, 'a> list) = BindingData.Bindings.mapMsgWithModel f bindings

  /// Map the message of a list of bindings via a covariant mapping.
  let mapMsg (f: 'a -> 'b) (bindings: Binding<'model, 'a> list) = BindingData.Bindings.mapMsg f bindings



module Binding =

  let internal subModelSelectedItemLast a b =
    BindingData.subModelSelectedItemLast a.Data b.Data


  /// Map the model of a binding via a contravariant mapping.
  let mapModel (f: 'a -> 'b) (binding: Binding<'b, 'msg>) = BindingData.Binding.mapModel f binding

  /// Map the message of a binding with access to the model via a covariant mapping.
  let mapMsgWithModel (f: 'model -> 'a -> 'b) (binding: Binding<'model, 'a>) = BindingData.Binding.mapMsgWithModel f binding

  /// Map the message of a binding via a covariant mapping.
  let mapMsg (f: 'a -> 'b) (binding: Binding<'model, 'a>) = BindingData.Binding.mapMsg f binding

  /// Set the message of a binding with access to the model.
  let SetMsgWithModel (f: 'model -> 'b) (binding: Binding<'model, 'a>) = BindingData.Binding.setMsgWithModel f binding

  /// Set the message of a binding.
  let setMsg (msg: 'b) (binding: Binding<'model, 'a>) = BindingData.Binding.setMsg msg binding


  /// Restrict the binding to models that satisfy the predicate after some model satisfies the predicate.
  let addSticky (predicate: 'model -> bool) (binding: Binding<'model, 'msg>) = BindingData.Binding.addSticky predicate binding

  /// <summary>
  ///   Adds caching to the given binding.  The cache holds a single value and
  ///   is invalidated after the given binding raises the
  ///   <c>PropertyChanged</c> event.
  /// </summary>
  /// <param name="binding">The binding to which caching is added.</param>
  let addCaching (binding: Binding<'model, 'msg>) : Binding<'model, 'msg> =
    binding
    |> BindingData.Binding.addCaching

  /// <summary>
  ///   Adds validation to the given binding using <c>INotifyDataErrorInfo</c>.
  /// </summary>
  /// <param name="validate">Returns the errors associated with the given model.</param>
  /// <param name="binding">The binding to which validation is added.</param>
  let addValidation (validate: 'model -> string list) (binding: Binding<'model, 'msg>) : Binding<'model, 'msg> =
    binding
    |> BindingData.Binding.addValidation validate

  /// <summary>
  ///   Adds laziness to the updating of the given binding. If the models are considered equal,
  ///   then updating of the given binding is skipped.
  /// </summary>
  /// <param name="equals">Updating skipped when this function returns <c>true</c>.</param>
  /// <param name="binding">The binding to which the laziness is added.</param>
  let addLazy (equals: 'model -> 'model -> bool) (binding: Binding<'model, 'msg>) : Binding<'model, 'msg> =
    binding
    |> BindingData.Binding.addLazy equals

  /// <summary>
  ///   Accepts a dispatch wrapping function.
  ///   This can be used to debounce, throttle, or limit this binding.
  ///   If more than one dispatching wrapping is added to a single binding,
  ///   then the dispatch wrapping functions are called in order
  ///   starting with the outer most or last such function and
  ///   finishing with the inner most or first such function.
  /// </summary>
  /// <param name="wrapDispatch">The function that will wrap the dispatch function.</param>
  /// <param name="binding">The binding to which the dispatch wrapping is added.</param>
  let addWrapDispatch (wrapDispatch: (obj -> unit) -> obj -> unit) (binding: Binding<'model, 'msg>) : Binding<'model, 'msg> =
    binding
    |> BindingData.Binding.addWrapDispatch wrapDispatch


  module OneWay =
    /// <summary>
    ///   Elemental instance of a one-way binding.
    /// </summary>
    let id<'a, 'msg> : string -> Binding<'a, 'msg> =
      { Get = box }
      |> OneWayData
      |> BaseBindingData
      |> createBinding

    /// <summary>
    ///   Creates a one-way binding to an optional value. The binding
    ///   automatically converts between a missing value in the model and
    ///   a <c>null</c> value in the view.
    /// </summary>
    let opt<'a, 'msg> : string -> Binding<'a option, 'msg> =
      id<obj, 'msg>
      >> mapModel BindingData.Option.box

    /// <summary>
    ///   Creates a one-way binding to an optional value. The binding
    ///   automatically converts between a missing value in the model and
    ///   a <c>null</c> value in the view.
    /// </summary>
    let vopt<'a, 'msg> : string -> Binding<'a voption, 'msg> =
      id<obj, 'msg>
      >> mapModel BindingData.ValueOption.box


  module OneWayToSource =
    /// <summary>
    ///   Elemental instance of a one-way-to-source binding.
    /// </summary>
    let id<'model, 'a> : string -> Binding<'model, 'a> =
      { Set = fun obj _ -> obj |> unbox }
      |> OneWayToSourceData
      |> BaseBindingData
      |> createBinding

    /// <summary>
    ///   Creates a one-way-to-source binding to an optional value. The binding
    ///   automatically converts between a missing value in the model and
    ///   a <c>null</c> value in the view.
    /// </summary>
    let vopt<'model, 'a> : string -> Binding<'model, 'a voption> =
      id<'model, obj>
      >> mapMsg BindingData.ValueOption.unbox

    /// <summary>
    ///   Creates a one-way-to-source binding to an optional value. The binding
    ///   automatically converts between a missing value in the model and
    ///   a <c>null</c> value in the view.
    /// </summary>
    let opt<'model, 'a> : string -> Binding<'model, 'a option> =
      id<'model, obj>
      >> mapMsg BindingData.Option.unbox


  module TwoWay =
    /// <summary>
    ///   Elemental instance of a two-way binding.
    /// </summary>
    let id<'a> : string -> Binding<'a, 'a> =
      { Get = box
        Set = fun obj _ -> unbox obj }
      |> TwoWayData
      |> BaseBindingData
      |> createBinding

    /// <summary>
    ///   Creates a one-way-to-source binding to an optional value. The binding
    ///   automatically converts between a missing value in the model and
    ///   a <c>null</c> value in the view.
    /// </summary>
    let vopt<'a> : string -> Binding<'a voption, 'a voption> =
      id<obj>
      >> mapModel BindingData.ValueOption.box
      >> mapMsg BindingData.ValueOption.unbox

    /// <summary>
    ///   Creates a one-way-to-source binding to an optional value. The binding
    ///   automatically converts between a missing value in the model and
    ///   a <c>null</c> value in the view.
    /// </summary>
    let opt<'a> : string -> Binding<'a option, 'a option> =
      id<obj>
      >> mapModel BindingData.Option.box
      >> mapMsg BindingData.Option.unbox


  module SubModel =

    let private mapMinorTypes
        (outMapBindingModel: 'bindingModel -> 'bindingModel0)
        (outMapBindingMsg: 'bindingMsg -> 'bindingMsg0)
        (inMapBindingModel: 'bindingModel0 -> 'bindingModel)
        (inMapBindingMsg: 'bindingMsg0 -> 'bindingMsg)
        (d: SubModelData<'model, 'msg, 'bindingModel, 'bindingMsg>) = {
      GetModel = d.GetModel >> ValueOption.map outMapBindingModel
      GetBindings = d.GetBindings >> Bindings.mapModel inMapBindingModel >> Bindings.mapMsg outMapBindingMsg
      ToMsg = fun m bMsg -> d.ToMsg m (inMapBindingMsg bMsg)
    }

    /// <summary>
    ///   Creates a binding to a sub-model/component. You typically bind this
    ///   to the <c>DataContext</c> of a <c>UserControl</c> or similar.
    /// </summary>
    /// <param name="bindings">Returns the bindings for the sub-model.</param>
    let vopt (bindings: unit -> Binding<'model, 'msg> list)
        : string -> Binding<'model voption, 'msg> =
      { GetModel = id
        GetBindings = bindings
        ToMsg = fun _ -> id }
      |> mapMinorTypes box box unbox unbox
      |> SubModelData
      |> BaseBindingData
      |> createBinding

    /// <summary>
    ///   Creates a binding to a sub-model/component. You typically bind this
    ///   to the <c>DataContext</c> of a <c>UserControl</c> or similar.
    /// </summary>
    /// <param name="bindings">Returns the bindings for the sub-model.</param>
    let opt (bindings: unit -> Binding<'model, 'msg> list)
        : string -> Binding<'model option, 'msg> =
      vopt bindings
      >> mapModel ValueOption.ofOption

    /// <summary>
    ///   Creates a binding to a sub-model/component. You typically bind this
    ///   to the <c>DataContext</c> of a <c>UserControl</c> or similar.
    /// </summary>
    /// <param name="bindings">Returns the bindings for the sub-model.</param>
    let required (bindings: unit -> Binding<'model, 'msg> list)
        : string -> Binding<'model, 'msg> =
      vopt bindings
      >> mapModel ValueSome


  module SelectedIndex =
    /// <summary>
    ///   Prebuilt binding intended for use with <code>Selector.SelectedIndex</code>.
    /// </summary>
    let vopt =
      TwoWay.id
      >> mapModel (ValueOption.defaultValue -1)
      >> mapMsg (fun i -> if i < 0 then ValueNone else ValueSome i)

    /// <summary>
    ///   Prebuilt binding intended for use with <code>Selector.SelectedIndex</code>.
    /// </summary>
    let opt =
      vopt
      >> mapModel ValueOption.ofOption
      >> mapMsg ValueOption.toOption



[<AbstractClass; Sealed>]
type Binding private () =

  /// <summary>
  ///   Prebuilt binding intended for use with <code>Selector.SelectedIndex</code>.
  /// </summary>
  /// <param name="get">Gets the selected index from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  static member selectedIndex
      (get: 'model -> int voption,
       set: int voption -> 'msg) =
    Binding.SelectedIndex.vopt
    >> Binding.mapModel get
    >> Binding.mapMsg set

  /// <summary>
  ///   Prebuilt binding intended for use with <code>Selector.SelectedIndex</code>.
  /// </summary>
  /// <param name="get">Gets the selected index from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  static member selectedIndex
      (get: 'model -> int option,
       set: int option -> 'msg) =
    Binding.SelectedIndex.opt
    >> Binding.mapModel get
    >> Binding.mapMsg set


  /// <summary>Creates a one-way binding.</summary>
  /// <param name="get">Gets the value from the model.</param>
  static member oneWay
      (get: 'model -> 'a)
      : string -> Binding<'model, 'msg> =
    Binding.OneWay.id<'a, 'msg>
    >> Binding.addLazy (=)
    >> Binding.mapModel get


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
    Binding.OneWay.opt<'a, 'msg>
    >> Binding.addLazy (=)
    >> Binding.mapModel get


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
    Binding.OneWay.vopt<'a, 'msg>
    >> Binding.addLazy (=)
    >> Binding.mapModel get


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
    Binding.OneWay.id<'b, 'msg>
    >> Binding.mapModel map
    >> Binding.addLazy equals
    >> Binding.mapModel get
    >> Binding.addCaching


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
    Binding.OneWay.opt<'b, 'msg>
    >> Binding.mapModel map
    >> Binding.addLazy equals
    >> Binding.mapModel get
    >> Binding.addCaching


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
    Binding.OneWay.vopt<'b, 'msg>
    >> Binding.mapModel map
    >> Binding.addLazy equals
    >> Binding.mapModel get
    >> Binding.addCaching


  /// <summary>Creates a one-way-to-source binding.</summary>
  /// <param name="set">Returns the message to dispatch.</param>
  static member oneWayToSource
      (set: 'a -> 'model -> 'msg)
      : string -> Binding<'model, 'msg> =
    Binding.OneWayToSource.id<'model, 'a>
    >> Binding.mapMsgWithModel (fun m v -> set v m)

  /// <summary>
  ///   Creates a one-way-to-source binding to an optional value. The binding
  ///   automatically converts between a missing value in the model and
  ///   a <c>null</c> value in the view.
  /// </summary>
  /// <param name="set">Returns the message to dispatch.</param>
  static member oneWayToSourceOpt
      (set: 'a option -> 'model -> 'msg)
      : string -> Binding<'model, 'msg> =
    Binding.OneWayToSource.opt
    >> Binding.mapMsgWithModel (fun m v -> set v m)

  /// <summary>
  ///   Creates a one-way-to-source binding to an optional value. The binding
  ///   automatically converts between a missing value in the model and
  ///   a <c>null</c> value in the view.
  /// </summary>
  /// <param name="set">Returns the message to dispatch.</param>
  static member oneWayToSourceOpt
      (set: 'a voption -> 'model -> 'msg)
      : string -> Binding<'model, 'msg> =
    Binding.OneWayToSource.vopt
    >> Binding.mapMsgWithModel (fun m v -> set v m)


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
    BindingData.OneWaySeqLazy.create get equals map itemEquals getId


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
    Binding.TwoWay.id<'a>
    >> Binding.addLazy (=)
    >> Binding.mapModel get
    >> Binding.mapMsgWithModel (fun m a -> set a m)


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
    Binding.TwoWay.opt<'a>
    >> Binding.addLazy (=)
    >> Binding.mapModel get
    >> Binding.mapMsgWithModel (fun m a -> set a m)


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
    Binding.TwoWay.vopt<'a>
    >> Binding.addLazy (=)
    >> Binding.mapModel get
    >> Binding.mapMsgWithModel (fun m a -> set a m)


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
    Binding.TwoWay.id<'a>
    >> Binding.addLazy (=)
    >> Binding.mapModel get
    >> Binding.mapMsgWithModel (fun m a -> set a m)
    >> Binding.addValidation validate


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
    Binding.TwoWay.id<'a>
    >> Binding.addLazy (=)
    >> Binding.mapModel get
    >> Binding.mapMsgWithModel (fun m a -> set a m)
    >> Binding.addValidation (validate >> ValueOption.toList)


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
    Binding.TwoWay.id<'a>
    >> Binding.addLazy (=)
    >> Binding.mapModel get
    >> Binding.mapMsgWithModel (fun m a -> set a m)
    >> Binding.addValidation (validate >> Option.toList)


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
    Binding.TwoWay.id<'a>
    >> Binding.addLazy (=)
    >> Binding.mapModel get
    >> Binding.mapMsgWithModel (fun m a -> set a m)
    >> Binding.addValidation (validate >> ValueOption.ofError >> ValueOption.toList)


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
    Binding.TwoWay.vopt<'a>
    >> Binding.addLazy (=)
    >> Binding.mapModel get
    >> Binding.mapMsgWithModel (fun m a -> set a m)
    >> Binding.addValidation validate


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
    Binding.TwoWay.vopt<'a>
    >> Binding.addLazy (=)
    >> Binding.mapModel get
    >> Binding.mapMsgWithModel (fun m a -> set a m)
    >> Binding.addValidation (validate >> ValueOption.toList)


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
    Binding.TwoWay.vopt<'a>
    >> Binding.addLazy (=)
    >> Binding.mapModel get
    >> Binding.mapMsgWithModel (fun m a -> set a m)
    >> Binding.addValidation (validate >> Option.toList)


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
    Binding.TwoWay.vopt<'a>
    >> Binding.addLazy (=)
    >> Binding.mapModel get
    >> Binding.mapMsgWithModel (fun m a -> set a m)
    >> Binding.addValidation (validate >> ValueOption.ofError >> ValueOption.toList)


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
    Binding.TwoWay.opt<'a>
    >> Binding.addLazy (=)
    >> Binding.mapModel get
    >> Binding.mapMsgWithModel (fun m a -> set a m)
    >> Binding.addValidation validate


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
    Binding.TwoWay.opt<'a>
    >> Binding.addLazy (=)
    >> Binding.mapModel get
    >> Binding.mapMsgWithModel (fun m a -> set a m)
    >> Binding.addValidation (validate >> ValueOption.toList)


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
    Binding.TwoWay.opt<'a>
    >> Binding.addLazy (=)
    >> Binding.mapModel get
    >> Binding.mapMsgWithModel (fun m a -> set a m)
    >> Binding.addValidation (validate >> Option.toList)


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
    Binding.TwoWay.opt<'a>
    >> Binding.addLazy (=)
    >> Binding.mapModel get
    >> Binding.mapMsgWithModel (fun m a -> set a m)
    >> Binding.addValidation (validate >> ValueOption.ofError >> ValueOption.toList)


  /// <summary>
  ///   Creates a <c>Command</c> binding that depends only on the model (not the
  ///   <c>CommandParameter</c>) and can always execute.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  static member cmd
      (exec: 'model -> 'msg)
      : string -> Binding<'model, 'msg> =
    BindingData.Cmd.create
      (exec >> ValueSome)
      (fun _ -> true)


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
    BindingData.Cmd.create
      (exec >> ValueSome)
      canExec


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
    BindingData.Cmd.create
      exec
      (exec >> ValueOption.isSome)


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
    BindingData.Cmd.create
      (exec >> ValueOption.ofOption)
      (exec >> Option.isSome)


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
    BindingData.Cmd.create
      (exec >> ValueOption.ofOk)
      (exec >> Result.isOk)


  /// <summary>
  ///   Creates a <c>Command</c> binding that depends on the
  ///   <c>CommandParameter</c>
  ///   and can always execute.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  static member cmdParam
      (exec: obj -> 'model -> 'msg)
      : string -> Binding<'model, 'msg> =
    BindingData.Cmd.createWithParam
      (fun p model -> exec p model |> ValueSome)
      (fun _ _ -> true)
      false


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
    BindingData.Cmd.createWithParam
      (fun p m -> exec p m |> ValueSome)
      canExec
      (defaultArg uiBoundCmdParam false)


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
    BindingData.Cmd.createWithParam
      exec
      (fun p m -> exec p m |> ValueOption.isSome)
      (defaultArg uiBoundCmdParam false)


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
    BindingData.Cmd.createWithParam
      (fun p m -> exec p m |> ValueOption.ofOption)
      (fun p m -> exec p m |> Option.isSome)
      (defaultArg uiBoundCmdParam false)


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
    BindingData.Cmd.createWithParam
      (fun p m -> exec p m |> ValueOption.ofOk)
      (fun p m -> exec p m |> Result.isOk)
      (defaultArg uiBoundCmdParam false)


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
  [<System.Obsolete("In version 5, this method will be removed.  Use \"Binding.SubModel.required\" followed by model and message mapping functions as needed.  For an example, see how this method is implemented.")>]
  static member subModel
      (getSubModel: 'model -> 'subModel,
       toBindingModel: 'model * 'subModel -> 'bindingModel,
       toMsg: 'bindingMsg -> 'msg,
       bindings: unit -> Binding<'bindingModel, 'bindingMsg> list)
      : string -> Binding<'model, 'msg> =
    Binding.SubModel.required bindings
    >> Binding.mapModel (fun m -> toBindingModel (m, getSubModel m))
    >> Binding.mapMsg toMsg

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
  [<System.Obsolete("In version 5, this method will be removed.  Use \"Binding.SubModel.required\" followed by model and message mapping functions as needed.  For an example, see how this method is implemented.")>]
  static member subModel
      (getSubModel: 'model -> 'subModel,
       toMsg: 'subMsg -> 'msg,
       bindings: unit -> Binding<'model * 'subModel, 'subMsg> list)
      : string -> Binding<'model, 'msg> =
    Binding.SubModel.required bindings
    >> Binding.mapModel (fun m -> (m, getSubModel m))
    >> Binding.mapMsg toMsg


  /// <summary>
  ///   Creates a binding to a sub-model/component that has its own bindings.
  ///   You typically bind this to the <c>DataContext</c> of a
  ///   <c>UserControl</c> or similar.
  /// </summary>
  /// <param name="getSubModel">Gets the sub-model from the model.</param>
  /// <param name="bindings">Returns the bindings for the sub-model.</param>
  [<System.Obsolete("In version 5, the type of the argument \"bindings\" will be changed to \"unit -> Binding<'model, 'msg> list\".  To avoid a compile error when upgrading, replace this method call with its implementation.")>]
  static member subModel
      (getSubModel: 'model -> 'subModel,
       bindings: unit -> Binding<'model * 'subModel, 'msg> list)
      : string -> Binding<'model, 'msg> =
    Binding.SubModel.required bindings
    >> Binding.mapModel (fun m -> (m, getSubModel m))


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
  [<System.Obsolete("In version 6, this method will be removed.  Its replacement method does not yet exist (because it will be one of the existing methods with a different type signature).  Either wait for version 5 when this message will change or replace this method with (a specialization of) the implementation of this method.")>]
  static member subModelOpt
      (getSubModel: 'model -> 'subModel voption,
       toBindingModel: 'model * 'subModel -> 'bindingModel,
       toMsg: 'bindingMsg -> 'msg,
       bindings: unit -> Binding<'bindingModel, 'bindingMsg> list,
       ?sticky: bool)
      : string -> Binding<'model, 'msg> =
    Binding.SubModel.vopt bindings
    >> if (defaultArg sticky false) then Binding.addLazy (fun previous next -> previous.IsSome && next.IsNone) else id
    >> Binding.mapModel (fun m -> getSubModel m |> ValueOption.map (fun sub -> toBindingModel (m, sub)))
    >> Binding.mapMsg toMsg


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
  [<System.Obsolete("In version 6, this method will be removed.  Its replacement method does not yet exist (because it will be one of the existing methods with a different type signature).  Either wait for version 5 when this message will change or replace this method with (a specialization of) the implementation of this method.")>]
  static member subModelOpt
      (getSubModel: 'model -> 'subModel option,
       toBindingModel: 'model * 'subModel -> 'bindingModel,
       toMsg: 'bindingMsg -> 'msg,
       bindings: unit -> Binding<'bindingModel, 'bindingMsg> list,
       ?sticky: bool)
      : string -> Binding<'model, 'msg> =
    Binding.SubModel.opt bindings
    >> if (defaultArg sticky false) then Binding.addLazy (fun previous next -> previous.IsSome && next.IsNone) else id
    >> Binding.mapModel (fun m -> getSubModel m |> Option.map (fun sub -> toBindingModel (m, sub)))
    >> Binding.mapMsg toMsg

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
  [<System.Obsolete("In version 6, this method will be removed.  Its replacement method does not yet exist (because it will be one of the existing methods with a different type signature).  Either wait for version 5 when this message will change or replace this method with (a specialization of) the implementation of this method.")>]
  static member subModelOpt
      (getSubModel: 'model -> 'subModel voption,
       toMsg: 'subMsg -> 'msg,
       bindings: unit -> Binding<'model * 'subModel, 'subMsg> list,
       ?sticky: bool)
      : string -> Binding<'model, 'msg> =
    Binding.SubModel.vopt bindings
    >> if (defaultArg sticky false) then Binding.addLazy (fun previous next -> previous.IsSome && next.IsNone) else id
    >> Binding.mapModel (fun m -> getSubModel m |> ValueOption.map (fun sub -> (m, sub)))
    >> Binding.mapMsg toMsg


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
  [<System.Obsolete("In version 6, this method will be removed.  Its replacement method does not yet exist (because it will be one of the existing methods with a different type signature).  Either wait for version 5 when this message will change or replace this method with (a specialization of) the implementation of this method.")>]
  static member subModelOpt
      (getSubModel: 'model -> 'subModel option,
       toMsg: 'subMsg -> 'msg,
       bindings: unit -> Binding<'model * 'subModel, 'subMsg> list,
       ?sticky: bool)
      : string -> Binding<'model, 'msg> =
    Binding.SubModel.opt bindings
    >> if (defaultArg sticky false) then Binding.addLazy (fun previous next -> previous.IsSome && next.IsNone) else id
    >> Binding.mapModel (fun m -> getSubModel m |> Option.map (fun sub -> (m, sub)))
    >> Binding.mapMsg toMsg


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
  [<System.Obsolete("In version 5, the type of the argument \"bindings\" will be changed to \"unit -> Binding<'model, 'msg> list\".  To avoid a compile error when upgrading, replace this method call with (a specialization of) its implementation.")>]
  static member subModelOpt
      (getSubModel: 'model -> 'subModel voption,
       bindings: unit -> Binding<'model * 'subModel, 'msg> list,
       ?sticky: bool)
      : string -> Binding<'model, 'msg> =
    Binding.SubModel.vopt bindings
    >> if (defaultArg sticky false) then Binding.addLazy (fun previous next -> previous.IsSome && next.IsNone) else id
    >> Binding.mapModel (fun m -> getSubModel m |> ValueOption.map (fun sub -> (m, sub)))


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
  [<System.Obsolete("In version 5, the type of the argument \"bindings\" will be changed to \"unit -> Binding<'model, 'msg> list\".  To avoid a compile error when upgrading, replace this method call with (a specialization of) its implementation.")>]
  static member subModelOpt
      (getSubModel: 'model -> 'subModel option,
       bindings: unit -> Binding<'model * 'subModel, 'msg> list,
       ?sticky: bool)
      : string -> Binding<'model, 'msg> =
    Binding.SubModel.opt bindings
    >> if (defaultArg sticky false) then Binding.addLazy (fun previous next -> previous.IsSome && next.IsNone) else id
    >> Binding.mapModel (fun m -> getSubModel m |> Option.map (fun sub -> (m, sub)))


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
  ///   If you don't need a sub-model, you can use
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
    BindingData.SubModelWin.create
      (fun m -> getState m |> WindowState.map (fun sub -> toBindingModel (m, sub)))
      bindings
      (fun _ -> toMsg)
      (fun m d -> upcast getWindow m d)
      (defaultArg isModal false)
      (fun _ -> defaultArg (onCloseRequested |> Option.map ValueSome) ValueNone)


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
  ///   If you don't need a sub-model, you can use
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
    BindingData.SubModelWin.create
      (fun m -> getState m |> WindowState.map (fun sub -> (m, sub)))
      bindings
      (fun _ -> toMsg)
      (fun m d -> upcast getWindow m d)
      (defaultArg isModal false)
      (fun _ -> defaultArg (onCloseRequested |> Option.map ValueSome) ValueNone)


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
    BindingData.SubModelWin.create
      (fun m -> getState m |> WindowState.map (fun sub -> (m, sub)))
      bindings
      (fun _ -> id)
      (fun m d -> upcast getWindow m d)
      (defaultArg isModal false)
      (fun _ -> defaultArg (onCloseRequested |> Option.map ValueSome) ValueNone)


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

  static member subModelSeq // TODO: make into function
      (getBindings: unit -> Binding<'model, 'msg> list)
      : string -> Binding<'model seq, int * 'msg> =
    BindingData.SubModelSeqUnkeyed.create getBindings

  static member subModelSeq // TODO: make into function
      (getBindings: unit -> Binding<'model, 'msg> list,
       getId: 'model -> 'id)
      : string -> Binding<'model seq, 'id * 'msg> =
    BindingData.SubModelSeqKeyed.create getBindings getId


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
    BindingData.SubModelSeqKeyed.create bindings getId
    >> Binding.mapModel (fun m -> getSubModels m |> Seq.map (fun sub -> toBindingModel (m, sub)))
    >> Binding.mapMsg toMsg


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
    BindingData.SubModelSeqKeyed.create bindings (snd >> getId)
    >> Binding.mapModel (fun m -> getSubModels m |> Seq.map (fun sub -> (m, sub)))
    >> Binding.mapMsg toMsg


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
    BindingData.SubModelSeqKeyed.create bindings (snd >> getId)
    >> Binding.mapModel (fun m -> getSubModels m |> Seq.map (fun sub -> (m, sub)))
    >> Binding.mapMsg snd


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
  ///   throw at runtime if the inferred <c>'id</c> type does not match the
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
    BindingData.SubModelSelectedItem.create get set subModelSeqBindingName


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
  ///   throw at runtime if the inferred <c>'id</c> type does not match the
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
    BindingData.SubModelSelectedItem.create
      (get >> ValueOption.ofOption)
      (ValueOption.toOption >> set)
      subModelSeqBindingName



// Some members are implemented as extensions to help overload resolution
[<AutoOpen>]
module Extensions =

  type Binding with

    /// <summary>Creates a one-way-to-source binding.</summary>
    /// <param name="set">Returns the message to dispatch.</param>
    static member oneWayToSource
        (set: 'a -> 'msg)
        : string -> Binding<'model, 'msg> =
      Binding.OneWayToSource.id<'model, 'a>
      >> Binding.mapMsg set

    /// <summary>
    ///   Creates a one-way-to-source binding to an optional value. The binding
    ///   automatically converts between a missing value in the model and
    ///   a <c>null</c> value in the view.
    /// </summary>
    /// <param name="set">Returns the message to dispatch.</param>
    static member oneWayToSourceOpt
        (set: 'a option -> 'msg)
        : string -> Binding<'model, 'msg> =
      Binding.OneWayToSource.opt
      >> Binding.mapMsg set

    /// <summary>
    ///   Creates a one-way-to-source binding to an optional value. The binding
    ///   automatically converts between a missing value in the model and
    ///   a <c>null</c> value in the view.
    /// </summary>
    /// <param name="set">Returns the message to dispatch.</param>
    static member oneWayToSourceOpt
        (set: 'a voption -> 'msg)
        : string -> Binding<'model, 'msg> =
      Binding.OneWayToSource.vopt
      >> Binding.mapMsg set


    /// <summary>Creates a two-way binding.</summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    static member twoWay
        (get: 'model -> 'a,
         set: 'a -> 'msg)
        : string -> Binding<'model, 'msg> =
      Binding.TwoWay.id<'a>
      >> Binding.addLazy (=)
      >> Binding.mapModel get
      >> Binding.mapMsg set


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
      Binding.TwoWay.opt<'a>
      >> Binding.addLazy (=)
      >> Binding.mapModel get
      >> Binding.mapMsg set

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
      Binding.TwoWay.vopt<'a>
      >> Binding.addLazy (=)
      >> Binding.mapModel get
      >> Binding.mapMsg set


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
      Binding.TwoWay.id<'a>
      >> Binding.addLazy (=)
      >> Binding.mapModel get
      >> Binding.mapMsg set
      >> Binding.addValidation validate


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
      Binding.TwoWay.id<'a>
      >> Binding.addLazy (=)
      >> Binding.mapModel get
      >> Binding.mapMsg set
      >> Binding.addValidation (validate >> ValueOption.toList)


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
      Binding.TwoWay.id<'a>
      >> Binding.addLazy (=)
      >> Binding.mapModel get
      >> Binding.mapMsg set
      >> Binding.addValidation (validate >> Option.toList)


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
      Binding.TwoWay.id<'a>
      >> Binding.addLazy (=)
      >> Binding.mapModel get
      >> Binding.mapMsg set
      >> Binding.addValidation (validate >> ValueOption.ofError >> ValueOption.toList)


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
      Binding.TwoWay.vopt<'a>
      >> Binding.addLazy (=)
      >> Binding.mapModel get
      >> Binding.mapMsg set
      >> Binding.addValidation validate


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
      Binding.TwoWay.vopt<'a>
      >> Binding.addLazy (=)
      >> Binding.mapModel get
      >> Binding.mapMsg set
      >> Binding.addValidation (validate >> ValueOption.toList)


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
      Binding.TwoWay.vopt<'a>
      >> Binding.addLazy (=)
      >> Binding.mapModel get
      >> Binding.mapMsg set
      >> Binding.addValidation (validate >> Option.toList)


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
      Binding.TwoWay.vopt<'a>
      >> Binding.addLazy (=)
      >> Binding.mapModel get
      >> Binding.mapMsg set
      >> Binding.addValidation (validate >> ValueOption.ofError >> ValueOption.toList)


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
      Binding.TwoWay.opt<'a>
      >> Binding.addLazy (=)
      >> Binding.mapModel get
      >> Binding.mapMsg set
      >> Binding.addValidation validate


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
      Binding.TwoWay.opt<'a>
      >> Binding.addLazy (=)
      >> Binding.mapModel get
      >> Binding.mapMsg set
      >> Binding.addValidation (validate >> ValueOption.toList)


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
      Binding.TwoWay.opt<'a>
      >> Binding.addLazy (=)
      >> Binding.mapModel get
      >> Binding.mapMsg set
      >> Binding.addValidation (validate >> Option.toList)


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
      Binding.TwoWay.opt<'a>
      >> Binding.addLazy (=)
      >> Binding.mapModel get
      >> Binding.mapMsg set
      >> Binding.addValidation (validate >> ValueOption.ofError >> ValueOption.toList)


    /// <summary>
    ///   Creates a <c>Command</c> binding that dispatches the specified message
    ///   and can always execute.
    /// </summary>
    /// <param name="exec">Returns the message to dispatch.</param>
    static member cmd
        (exec: 'msg)
        : string -> Binding<'model, 'msg> =
      BindingData.Cmd.create
        (fun _ -> exec |> ValueSome)
        (fun _ -> true)


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
      BindingData.Cmd.create
        (fun _ -> exec |> ValueSome)
        canExec


    /// <summary>
    ///   Creates a <c>Command</c> binding that depends on the
    ///   <c>CommandParameter</c>
    ///   and can always execute.
    /// </summary>
    /// <param name="exec">Returns the message to dispatch.</param>
    static member cmdParam
        (exec: obj -> 'msg)
        : string -> Binding<'model, 'msg> =
      BindingData.Cmd.createWithParam
        (fun p _ -> exec p |> ValueSome)
        (fun _ _ -> true)
        false


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
      BindingData.Cmd.createWithParam
        (fun p _ -> exec p)
        (fun p _ -> exec p |> ValueOption.isSome)
        (defaultArg uiBoundCmdParam false)


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
      BindingData.Cmd.createWithParam
        (fun p _ -> exec p |> ValueOption.ofOption)
        (fun p _ -> exec p |> Option.isSome)
        (defaultArg uiBoundCmdParam false)


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
      BindingData.Cmd.createWithParam
        (fun p _ -> exec p |> ValueOption.ofOk)
        (fun p _ -> exec p |> Result.isOk)
        (defaultArg uiBoundCmdParam false)


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
      BindingData.Cmd.createWithParam
        (fun p _ -> exec p |> ValueSome)
        (fun p _ -> canExec p)
        (defaultArg uiBoundCmdParam false)


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
    ///   will throw at runtime if the inferred <c>'id</c> type does not
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
      BindingData.SubModelSelectedItem.create
        get
        (fun id _ -> id |> set)
        subModelSeqBindingName


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
    ///   will throw at runtime if the inferred <c>'id</c> type does not
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
      BindingData.SubModelSelectedItem.create
        (get >> ValueOption.ofOption)
        (fun id _ -> id |> ValueOption.toOption |> set)
        subModelSeqBindingName