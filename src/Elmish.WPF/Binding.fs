namespace Elmish.WPF

open System.Windows

open Elmish

[<RequireQualifiedAccess>]
type WindowState<'model> =
  | Closed
  | Hidden of 'model
  | Visible of 'model

module WindowState =

  let map (f: 'a -> 'b) (state: WindowState<'a>) =
    match state with
    | WindowState.Closed -> WindowState.Closed
    | WindowState.Hidden m -> WindowState.Hidden (f m)
    | WindowState.Visible m -> WindowState.Visible (f m)

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


type internal OneWayData<'model, 'a> = {
  Get: 'model -> 'a
}

type internal OneWayLazyData<'model, 'a, 'b> = {
  Get: 'model -> 'a
  Map: 'a -> 'b
  Equals: 'a -> 'a -> bool
}

type internal OneWaySeqLazyData<'model, 'a, 'b, 'id> = {
  Get: 'model -> 'a
  Map: 'a -> 'b seq
  Equals: 'a -> 'a -> bool
  GetId: 'b -> 'id
  ItemEquals: 'b -> 'b -> bool
}

type internal TwoWayData<'model, 'msg, 'a> = {
  Get: 'model -> 'a
  Set: 'a -> 'model -> 'msg
  WrapDispatch: Dispatch<'msg> -> Dispatch<'msg>
}

type internal TwoWayValidateData<'model, 'msg, 'a> = {
  Get: 'model -> 'a
  Set: 'a -> 'model -> 'msg
  Validate: 'model -> string list
  WrapDispatch: Dispatch<'msg> -> Dispatch<'msg>
}

type internal CmdData<'model, 'msg> = {
  Exec: 'model -> 'msg voption
  CanExec: 'model -> bool
  WrapDispatch: Dispatch<'msg> -> Dispatch<'msg>
}

type internal CmdParamData<'model, 'msg> = {
  Exec: obj -> 'model -> 'msg voption
  CanExec: obj -> 'model -> bool
  AutoRequery: bool
  WrapDispatch: Dispatch<'msg> -> Dispatch<'msg>
}

type internal SubModelSelectedItemData<'model, 'msg, 'id> = {
  Get: 'model -> 'id voption
  Set: 'id voption -> 'model -> 'msg
  SubModelSeqBindingName: string
  WrapDispatch: Dispatch<'msg> -> Dispatch<'msg>
}

type internal SubModelData<'model, 'msg, 'bindingModel, 'bindingMsg> = {
  GetModel: 'model -> 'bindingModel voption
  GetBindings: unit -> Binding<'bindingModel, 'bindingMsg> list
  ToMsg: 'bindingMsg -> 'msg
  Sticky: bool
}

and internal SubModelWinData<'model, 'msg, 'bindingModel, 'bindingMsg> = {
  GetState: 'model -> WindowState<'bindingModel>
  GetBindings: unit -> Binding<'bindingModel, 'bindingMsg> list
  ToMsg: 'bindingMsg -> 'msg
  GetWindow: 'model -> Dispatch<'msg> -> Window
  IsModal: bool
  OnCloseRequested: 'msg voption
}

and internal SubModelSeqData<'model, 'msg, 'bindingModel, 'bindingMsg, 'id> = {
  GetModels: 'model -> 'bindingModel seq
  GetId: 'bindingModel -> 'id
  GetBindings: unit -> Binding<'bindingModel, 'bindingMsg> list
  ToMsg: 'id * 'bindingMsg -> 'msg
}


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

  let boxWrapDispatch
      (unboxMsg: 'boxedMsg -> 'msg)
      (boxMsg: 'msg -> 'boxedMsg)
      (strongWrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : Dispatch<'boxedMsg> -> Dispatch<'boxedMsg> =
    ((>>) boxMsg) >> strongWrapDispatch >> ((>>) unboxMsg)

  let boxMsg
      (unboxMsg: 'boxedMsg -> 'msg)
      (boxMsg: 'msg -> 'boxedMsg)
      : BindingData<'model, 'msg> -> BindingData<'model, 'boxedMsg> = function
    | OneWayData d -> d |> OneWayData
    | OneWayLazyData d -> d |> OneWayLazyData
    | OneWaySeqLazyData d -> d |> OneWaySeqLazyData
    | TwoWayData d -> TwoWayData {
        Get = d.Get
        Set = fun v m -> d.Set v m |> boxMsg
        WrapDispatch = boxWrapDispatch unboxMsg boxMsg d.WrapDispatch
      }
    | TwoWayValidateData d -> TwoWayValidateData {
        Get = d.Get
        Set = fun v m -> d.Set v m |> boxMsg
        Validate = unbox >> d.Validate
        WrapDispatch = boxWrapDispatch unboxMsg boxMsg d.WrapDispatch
      }
    | CmdData d -> CmdData {
        Exec = d.Exec >> ValueOption.map boxMsg
        CanExec = d.CanExec
        WrapDispatch = boxWrapDispatch unboxMsg boxMsg d.WrapDispatch
      }
    | CmdParamData d -> CmdParamData {
        Exec = fun p m -> d.Exec p m |> ValueOption.map boxMsg
        CanExec = fun p m -> d.CanExec p m
        AutoRequery = d.AutoRequery
        WrapDispatch = boxWrapDispatch unboxMsg boxMsg d.WrapDispatch
      }
    | SubModelData d -> SubModelData {
        GetModel = d.GetModel
        GetBindings = d.GetBindings
        ToMsg = d.ToMsg >> boxMsg
        Sticky = d.Sticky
      }
    | SubModelWinData d -> SubModelWinData {
        GetState = d.GetState
        GetBindings = d.GetBindings
        ToMsg = d.ToMsg >> boxMsg
        GetWindow = fun m dispatch -> d.GetWindow m (boxMsg >> dispatch)
        IsModal = d.IsModal
        OnCloseRequested = d.OnCloseRequested |> ValueOption.map boxMsg
      }
    | SubModelSeqData d -> SubModelSeqData {
        GetModels = d.GetModels
        GetId = d.GetId
        GetBindings = d.GetBindings
        ToMsg = d.ToMsg >> boxMsg
      }
    | SubModelSelectedItemData d -> SubModelSelectedItemData {
        Get = d.Get
        Set = fun v m -> d.Set v m |> boxMsg
        SubModelSeqBindingName = d.SubModelSeqBindingName
        WrapDispatch = boxWrapDispatch unboxMsg boxMsg d.WrapDispatch
      }

  let mapModel f data =
    let binaryHelper binary x m = (x, f m) ||> binary
    match data with
    | OneWayData d ->
        { Get = f >> d.Get
        } |> OneWayData
    | OneWayLazyData d ->
        { Get = f >> d.Get
          Map = d.Map;
          Equals = d.Equals
        } |> OneWayLazyData
    | OneWaySeqLazyData d ->
        { Get = f >> d.Get
          Map = d.Map
          Equals = d.Equals
          GetId = d.GetId
          ItemEquals = d.ItemEquals
        } |> OneWaySeqLazyData
    | TwoWayData d ->
        { Get = f >> d.Get
          Set = binaryHelper d.Set
          WrapDispatch = d.WrapDispatch
        } |> TwoWayData
    | TwoWayValidateData d ->
        { Get = f >> d.Get
          Set = binaryHelper d.Set
          Validate = f >> d.Validate
          WrapDispatch = d.WrapDispatch
        } |> TwoWayValidateData
    | CmdData d ->
        { Exec = f >> d.Exec
          CanExec = f >> d.CanExec
          WrapDispatch = d.WrapDispatch
        } |> CmdData
    | CmdParamData d ->
        { Exec = binaryHelper d.Exec
          CanExec = binaryHelper d.CanExec
          AutoRequery = d.AutoRequery
          WrapDispatch = d.WrapDispatch
        } |> CmdParamData
    | SubModelData d ->
        { GetModel = f >> d.GetModel
          GetBindings = d.GetBindings
          ToMsg = d.ToMsg
          Sticky = d.Sticky
        } |> SubModelData
    | SubModelWinData d ->
        { GetState = f >> d.GetState
          GetBindings = d.GetBindings
          ToMsg = d.ToMsg
          GetWindow = f >> d.GetWindow
          IsModal = d.IsModal
          OnCloseRequested = d.OnCloseRequested
        } |> SubModelWinData
    | SubModelSeqData d ->
        { GetModels = f >> d.GetModels
          GetId = d.GetId
          GetBindings = d.GetBindings
          ToMsg = d.ToMsg
        } |> SubModelSeqData
    | SubModelSelectedItemData d ->
        { Get = f >> d.Get
          Set = binaryHelper d.Set
          SubModelSeqBindingName = d.SubModelSeqBindingName
          WrapDispatch = d.WrapDispatch
        } |> SubModelSelectedItemData


module internal Binding =

  let mapData f binding =
    { Name = binding.Name
      Data = binding.Data |> f }

  let mapModel f = f |> BindingData.mapModel |> mapData

  let subModelSelectedItemLast a b =
    BindingData.subModelSelectedItemLast a.Data b.Data


module internal Bindings =

  let mapModel f bindings = bindings |> List.map (Binding.mapModel f)



[<AutoOpen>]
module internal Helpers =

  let createBinding data name =
    { Name = name
      Data = data }

  let boxBinding (binding: Binding<'a, 'b>) : Binding<obj, obj> =
    { Name = binding.Name
      Data = BindingData.boxMsg unbox box binding.Data }
    |> Binding.mapModel unbox



[<AbstractClass; Sealed>]
type Binding private () =


  /// <summary>Creates a one-way binding.</summary>
  /// <param name="get">Gets the value from the model.</param>
  static member oneWay
      (get: 'model -> 'a)
      : string -> Binding<'model, 'msg> =
    OneWayData {
      Get = get >> box
    } |> createBinding


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
    OneWayData {
      Get = get >> Option.map box >> Option.toObj
    } |> createBinding


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
    OneWayData {
      Get = get >> ValueOption.map box >> ValueOption.toObj
    } |> createBinding


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
    OneWayLazyData {
      Get = get >> box
      Map = unbox<'a> >> map >> box
      Equals = fun a b -> equals (unbox<'a> a) (unbox<'a> b)
    } |> createBinding


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
    OneWayLazyData {
      Get = get >> box
      Map = unbox<'a> >> map >> Option.map box >> Option.toObj
      Equals = fun a b -> equals (unbox<'a> a) (unbox<'a> b)
    } |> createBinding


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
    OneWayLazyData {
      Get = get >> box
      Map = unbox<'a> >> map >> ValueOption.map box >> ValueOption.toObj
      Equals = fun a b -> equals (unbox<'a> a) (unbox<'a> b)
    } |> createBinding


  /// <summary>
  ///   Creates a one-way binding to a sequence of items, each uniquely
  ///   identified by the value returned by <paramref name="getId"/>. The
  ///   binding is backed by a persistent <c>ObservableCollection</c>, so only
  ///   changed items (as determined by <paramref name="itemEquals" />) will be
  ///   replaced. If the items are complex and you want them updated instead of
  ///   replaced, consider using <see cref="subModelSeq" />.
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
    OneWaySeqLazyData {
      Get = box
      Map = unbox<'model> >> get >> Seq.map box
      Equals = fun _ _ -> false
      GetId = unbox<'a> >> getId >> box
      ItemEquals = fun a b -> itemEquals (unbox<'a> a) (unbox<'a> b)
    } |> createBinding


  /// <summary>
  ///   Creates a one-way binding to a sequence of items, each uniquely
  ///   identified by the value returned by <paramref name="getId"/>. The
  ///   binding will only be updated if the output of <paramref name="get" />
  ///   changes, as determined by <paramref name="equals" />. The binding is
  ///   backed by a persistent
  ///   <c>ObservableCollection</c>, so only changed items (as determined by
  ///   <paramref name="itemEquals" />) will be replaced. If the items are
  ///   complex and you want them updated instead of replaced, consider using
  ///   <see cref="subModelSeq" />.
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
    OneWaySeqLazyData {
      Get = get >> box
      Map = unbox<'a> >> map >> Seq.map box
      Equals = fun x y -> equals (unbox<'a> x) (unbox<'a> y)
      GetId = unbox<'b> >> getId >> box
      ItemEquals = fun x y -> itemEquals (unbox<'b> x) (unbox<'b> y)
    } |> createBinding


  /// <summary>Creates a two-way binding.</summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  static member twoWay
      (get: 'model -> 'a,
       set: 'a -> 'model -> 'msg,
       ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    TwoWayData {
      Get = get >> box
      Set = unbox<'a> >> set
      WrapDispatch = defaultArg wrapDispatch id
    } |> createBinding


  /// <summary>
  ///   Creates a two-way binding to an optional value. The binding
  ///   automatically converts between the optional source value and an
  ///   unwrapped (possibly <c>null</c>) value on the view side.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  static member twoWayOpt
      (get: 'model -> 'a option,
       set: 'a option -> 'model -> 'msg,
       ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    TwoWayData {
      Get = get >> Option.map box >> Option.toObj
      Set = Option.ofObj >> Option.map unbox<'a> >> set
      WrapDispatch = defaultArg wrapDispatch id
    } |> createBinding


  /// <summary>
  ///   Creates a two-way binding to an optional value. The binding
  ///   automatically converts between the optional source value and an
  ///   unwrapped (possibly <c>null</c>) value on the view side.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  static member twoWayOpt
      (get: 'model -> 'a voption,
       set: 'a voption -> 'model -> 'msg,
       ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    TwoWayData {
      Get = get >> ValueOption.map box >> ValueOption.toObj
      Set = ValueOption.ofObj >> ValueOption.map unbox<'a> >> set
      WrapDispatch = defaultArg wrapDispatch id
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
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  static member twoWayValidate
      (get: 'model -> 'a,
       set: 'a -> 'model -> 'msg,
       validate: 'model -> string list,
       ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    TwoWayValidateData {
      Get = get >> box
      Set = unbox<'a> >> set
      Validate = validate
      WrapDispatch = defaultArg wrapDispatch id
    } |> createBinding


  /// <summary>
  ///   Creates a two-way binding with validation using
  ///   <c>INotifyDataErrorInfo</c>.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="validate">
  ///   Returns the validation message from the updated model.
  /// </param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  static member twoWayValidate
      (get: 'model -> 'a,
       set: 'a -> 'model -> 'msg,
       validate: 'model -> string voption,
       ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    TwoWayValidateData {
      Get = get >> box
      Set = unbox<'a> >> set
      Validate = validate >> ValueOption.toList
      WrapDispatch = defaultArg wrapDispatch id
    } |> createBinding


  /// <summary>
  ///   Creates a two-way binding with validation using
  ///   <c>INotifyDataErrorInfo</c>.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="validate">
  ///   Returns the validation message from the updated model.
  /// </param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  static member twoWayValidate
      (get: 'model -> 'a,
       set: 'a -> 'model -> 'msg,
       validate: 'model -> string option,
       ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    TwoWayValidateData {
      Get = get >> box
      Set = unbox<'a> >> set
      Validate = validate >> Option.toList
      WrapDispatch = defaultArg wrapDispatch id
    } |> createBinding


  /// <summary>
  ///   Creates a two-way binding with validation using
  ///   <c>INotifyDataErrorInfo</c>.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="validate">
  ///   Returns the validation message from the updated model.
  /// </param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  static member twoWayValidate
      (get: 'model -> 'a,
       set: 'a -> 'model -> 'msg,
       validate: 'model -> Result<'ignored, string>,
       ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    TwoWayValidateData {
      Get = get >> box
      Set = unbox<'a> >> set
      Validate = validate >> ValueOption.ofError >> ValueOption.toList
      WrapDispatch = defaultArg wrapDispatch id
    } |> createBinding


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
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  static member twoWayOptValidate
      (get: 'model -> 'a voption,
       set: 'a voption -> 'model -> 'msg,
       validate: 'model -> string list,
       ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    TwoWayValidateData {
      Get = get >> ValueOption.map box >> ValueOption.toObj
      Set = ValueOption.ofObj >> ValueOption.map unbox<'a> >> set
      Validate = validate
      WrapDispatch = defaultArg wrapDispatch id
    } |> createBinding


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
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  static member twoWayOptValidate
      (get: 'model -> 'a voption,
       set: 'a voption -> 'model -> 'msg,
       validate: 'model -> string voption,
       ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    TwoWayValidateData {
      Get = get >> ValueOption.map box >> ValueOption.toObj
      Set = ValueOption.ofObj >> ValueOption.map unbox<'a> >> set
      Validate = validate >> ValueOption.toList
      WrapDispatch = defaultArg wrapDispatch id
    } |> createBinding


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
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  static member twoWayOptValidate
      (get: 'model -> 'a voption,
       set: 'a voption -> 'model -> 'msg,
       validate: 'model -> string option,
       ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    TwoWayValidateData {
      Get = get >> ValueOption.map box >> ValueOption.toObj
      Set = ValueOption.ofObj >> ValueOption.map unbox<'a> >> set
      Validate = validate >> Option.toList
      WrapDispatch = defaultArg wrapDispatch id
    } |> createBinding


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
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  static member twoWayOptValidate
      (get: 'model -> 'a voption,
       set: 'a voption -> 'model -> 'msg,
       validate: 'model -> Result<'ignored, string>,
       ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    TwoWayValidateData {
      Get = get >> ValueOption.map box >> ValueOption.toObj
      Set = ValueOption.ofObj >> ValueOption.map unbox<'a> >> set
      Validate = validate >> ValueOption.ofError >> ValueOption.toList
      WrapDispatch = defaultArg wrapDispatch id
    } |> createBinding


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
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  static member twoWayOptValidate
      (get: 'model -> 'a option,
       set: 'a option -> 'model -> 'msg,
       validate: 'model -> string list,
       ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    TwoWayValidateData {
      Get = get >> Option.map box >> Option.toObj
      Set = Option.ofObj >> Option.map unbox<'a> >> set
      Validate = validate
      WrapDispatch = defaultArg wrapDispatch id
    } |> createBinding


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
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  static member twoWayOptValidate
      (get: 'model -> 'a option,
       set: 'a option -> 'model -> 'msg,
       validate: 'model -> string voption,
       ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    TwoWayValidateData {
      Get = get >> Option.map box >> Option.toObj
      Set = Option.ofObj >> Option.map unbox<'a> >> set
      Validate = validate >> ValueOption.toList
      WrapDispatch = defaultArg wrapDispatch id
    } |> createBinding


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
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  static member twoWayOptValidate
      (get: 'model -> 'a option,
       set: 'a option -> 'model -> 'msg,
       validate: 'model -> string option,
       ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    TwoWayValidateData {
      Get = get >> Option.map box >> Option.toObj
      Set = Option.ofObj >> Option.map unbox<'a> >> set
      Validate = validate >> Option.toList
      WrapDispatch = defaultArg wrapDispatch id
    } |> createBinding


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
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  static member twoWayOptValidate
      (get: 'model -> 'a option,
       set: 'a option -> 'model -> 'msg,
       validate: 'model -> Result<'ignored, string>,
       ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    TwoWayValidateData {
      Get = get >> Option.map box >> Option.toObj
      Set = Option.ofObj >> Option.map unbox<'a> >> set
      Validate = validate >> ValueOption.ofError >> ValueOption.toList
      WrapDispatch = defaultArg wrapDispatch id
    } |> createBinding


  /// <summary>
  ///   Creates a <c>Command</c> binding that depends only on the model (not the
  ///   <c>CommandParameter</c>) and can always execute.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  static member cmd
      (exec: 'model -> 'msg,
       ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    CmdData {
      Exec = exec >> ValueSome
      CanExec = fun _ -> true
      WrapDispatch = defaultArg wrapDispatch id
    } |> createBinding


  /// <summary>
  ///   Creates a conditional <c>Command</c> binding that depends only on the
  ///   model (not the <c>CommandParameter</c>) and can execute if <paramref
  ///   name="canExec" />
  ///   returns <c>true</c>.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  /// <param name="canExec">Indicates whether the command can execute.</param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  static member cmdIf
      (exec: 'model -> 'msg,
       canExec: 'model -> bool,
       ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    CmdData {
      Exec = exec >> ValueSome
      CanExec = canExec
      WrapDispatch = defaultArg wrapDispatch id
    } |> createBinding


  /// <summary>
  ///   Creates a conditional <c>Command</c> binding that depends only on the
  ///   model (not the <c>CommandParameter</c>) and can execute if <paramref
  ///   name="exec" />
  ///   returns <c>ValueSome</c>.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  static member cmdIf
      (exec: 'model -> 'msg voption,
       ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    CmdData {
      Exec = exec
      CanExec = exec >> ValueOption.isSome
      WrapDispatch = defaultArg wrapDispatch id
    } |> createBinding


  /// <summary>
  ///   Creates a conditional <c>Command</c> binding that depends only on the
  ///   model (not the <c>CommandParameter</c>) and can execute if <paramref
  ///   name="exec" />
  ///   returns <c>Some</c>.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  static member cmdIf
      (exec: 'model -> 'msg option,
       ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    CmdData {
      Exec = exec >> ValueOption.ofOption
      CanExec = exec >> Option.isSome
      WrapDispatch = defaultArg wrapDispatch id
    } |> createBinding


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
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  static member cmdIf
      (exec: 'model -> Result<'msg, 'ignored>,
       ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    CmdData {
      Exec = exec >> ValueOption.ofOk
      CanExec = exec >> Result.isOk
      WrapDispatch = defaultArg wrapDispatch id
    } |> createBinding


  /// <summary>
  ///   Creates a <c>Command</c> binding that depends on the
  ///   <c>CommandParameter</c>
  ///   and can always execute.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  static member cmdParam
      (exec: obj -> 'model -> 'msg,
       ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    CmdParamData {
      Exec = fun p model -> exec p model |> ValueSome
      CanExec = fun _ _ -> true
      AutoRequery = false
      WrapDispatch = defaultArg wrapDispatch id
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
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  static member cmdParamIf
      (exec: obj -> 'model -> 'msg,
       canExec: obj -> 'model -> bool,
       ?uiBoundCmdParam: bool,
       ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    CmdParamData {
      Exec = fun p m -> exec p m |> ValueSome
      CanExec = canExec
      AutoRequery = defaultArg uiBoundCmdParam false
      WrapDispatch = defaultArg wrapDispatch id
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
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  static member cmdParamIf
      (exec: obj -> 'model -> 'msg voption,
       ?uiBoundCmdParam: bool,
       ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    CmdParamData {
      Exec = exec
      CanExec = fun p m -> exec p m |> ValueOption.isSome
      AutoRequery = defaultArg uiBoundCmdParam false
      WrapDispatch = defaultArg wrapDispatch id
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
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  static member cmdParamIf
      (exec: obj -> 'model -> 'msg option,
       ?uiBoundCmdParam: bool,
       ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    CmdParamData {
      Exec = fun p m -> exec p m |> ValueOption.ofOption
      CanExec = fun p m -> exec p m |> Option.isSome
      AutoRequery = defaultArg uiBoundCmdParam false
      WrapDispatch = defaultArg wrapDispatch id
    } |> createBinding


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
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  static member cmdParamIf
      (exec: obj -> 'model -> Result<'msg, 'ignored>,
       ?uiBoundCmdParam: bool,
       ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    CmdParamData {
      Exec = fun p m -> exec p m |> ValueOption.ofOk
      CanExec = fun p m -> exec p m |> Result.isOk
      AutoRequery = defaultArg uiBoundCmdParam false
      WrapDispatch = defaultArg wrapDispatch id
    } |> createBinding


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
    SubModelData {
      GetModel = fun m -> toBindingModel (m, getSubModel m) |> box |> ValueSome
      GetBindings = bindings >> List.map boxBinding
      ToMsg = unbox<'bindingMsg> >> toMsg
      Sticky = false
    } |> createBinding


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
    SubModelData {
      GetModel = fun m -> (m, getSubModel m) |> box |> ValueSome
      GetBindings = bindings >> List.map boxBinding
      ToMsg = unbox<'subMsg> >> toMsg
      Sticky = false
    } |> createBinding


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
    SubModelData {
      GetModel = fun m -> (m, getSubModel m) |> box |> ValueSome
      GetBindings = bindings >> List.map boxBinding
      ToMsg = unbox<'msg>
      Sticky = false
    } |> createBinding


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
    SubModelData {
      GetModel = fun m ->
        getSubModel m |> ValueOption.map (fun sub -> toBindingModel (m, sub) |> box)
      GetBindings = bindings >> List.map boxBinding
      ToMsg = unbox<'bindingMsg> >> toMsg
      Sticky = defaultArg sticky false
    } |> createBinding


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
    SubModelData {
      GetModel = fun m ->
        getSubModel m
        |> ValueOption.ofOption
        |> ValueOption.map (fun sub -> toBindingModel (m, sub) |> box)
      GetBindings = bindings >> List.map boxBinding
      ToMsg = unbox<'bindingMsg> >> toMsg
      Sticky = defaultArg sticky false
    } |> createBinding


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
    SubModelData {
      GetModel = fun m ->
        getSubModel m |> ValueOption.map (fun sub -> (m, sub) |> box)
      GetBindings = bindings >> List.map boxBinding
      ToMsg = unbox<'subMsg> >> toMsg
      Sticky = defaultArg sticky false
    } |> createBinding


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
    SubModelData {
      GetModel = fun m ->
        getSubModel m
        |> ValueOption.ofOption
        |> ValueOption.map (fun sub -> (m, sub) |> box)
      GetBindings = bindings >> List.map boxBinding
      ToMsg = unbox<'subMsg> >> toMsg
      Sticky = defaultArg sticky false
    } |> createBinding


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
    SubModelData {
      GetModel = fun m ->
        getSubModel m |> ValueOption.map (fun sub -> (m, sub) |> box)
      GetBindings = bindings >> List.map boxBinding
      ToMsg = unbox<'msg>
      Sticky = defaultArg sticky false
    } |> createBinding


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
    SubModelData {
      GetModel = fun m ->
        getSubModel m
        |> ValueOption.ofOption
        |> ValueOption.map (fun sub -> (m, sub) |> box)
      GetBindings = bindings >> List.map boxBinding
      ToMsg = unbox<'msg>
      Sticky = defaultArg sticky false
    } |> createBinding

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
    SubModelWinData {
      GetState = fun m ->
        getState m |> WindowState.map (fun sub -> toBindingModel (m, sub) |> box)
      GetBindings = bindings >> List.map boxBinding
      ToMsg = unbox<'bindingMsg> >> toMsg
      GetWindow = fun m d -> upcast getWindow m d
      IsModal = defaultArg isModal false
      OnCloseRequested = defaultArg (onCloseRequested |> Option.map ValueSome) ValueNone
    } |> createBinding


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
    SubModelWinData {
      GetState = fun m ->
        getState m |> WindowState.map (fun sub -> (m, sub) |> box)
      GetBindings = bindings >> List.map boxBinding
      ToMsg = unbox<'subMsg> >> toMsg
      GetWindow = fun m d -> upcast getWindow m d
      IsModal = defaultArg isModal false
      OnCloseRequested = defaultArg (onCloseRequested |> Option.map ValueSome) ValueNone
    } |> createBinding


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
    SubModelWinData {
      GetState = fun m ->
        getState m |> WindowState.map (fun sub -> (m, sub) |> box)
      GetBindings = bindings >> List.map boxBinding
      ToMsg = unbox<'msg>
      GetWindow = fun m d -> upcast getWindow m d
      IsModal = defaultArg isModal false
      OnCloseRequested = defaultArg (onCloseRequested |> Option.map ValueSome) ValueNone
    } |> createBinding


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
    SubModelSeqData {
      GetModels = fun m ->
        m |> getSubModels |> Seq.map (fun sub -> toBindingModel (m, sub) |> box)
      GetId = unbox<'bindingModel> >> getId >> box
      GetBindings = bindings >> List.map boxBinding
      ToMsg = fun (id, msg) -> toMsg (unbox<'id> id, unbox<'bindingMsg> msg)
    } |> createBinding


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
    SubModelSeqData {
      GetModels = fun m -> m |> getSubModels |> Seq.map (fun sub -> (m, sub) |> box)
      GetId = unbox<'model * 'subModel> >> snd >> getId >> box
      GetBindings = bindings >> List.map boxBinding
      ToMsg = fun (id, msg) -> toMsg (unbox<'id> id, unbox<'subMsg> msg)
    } |> createBinding


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
    SubModelSeqData {
      GetModels = fun m -> m |> getSubModels |> Seq.map (fun sub -> (m, sub) |> box)
      GetId = unbox<'model * 'subModel> >> snd >> getId >> box
      GetBindings = bindings >> List.map boxBinding
      ToMsg = fun (_, msg) -> unbox<'msg> msg
    } |> createBinding


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
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  static member subModelSelectedItem
      (subModelSeqBindingName: string,
       get: 'model -> 'id voption,
       set: 'id voption -> 'model -> 'msg,
       ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    SubModelSelectedItemData {
      Get = get >> ValueOption.map box
      Set = ValueOption.map unbox<'id> >> set
      SubModelSeqBindingName = subModelSeqBindingName
      WrapDispatch = defaultArg wrapDispatch id
    } |> createBinding


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
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  static member subModelSelectedItem
      (subModelSeqBindingName: string,
       get: 'model -> 'id option,
       set: 'id option -> 'model -> 'msg,
       ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    SubModelSelectedItemData {
      Get = get >> ValueOption.ofOption >> ValueOption.map box
      Set = ValueOption.map unbox<'id> >> ValueOption.toOption >> set
      SubModelSeqBindingName = subModelSeqBindingName
      WrapDispatch = defaultArg wrapDispatch id
    } |> createBinding



// Some members are implemented as extensions to help overload resolution
[<AutoOpen>]
module Extensions =

  type Binding with

    /// <summary>Creates a two-way binding.</summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    static member twoWay
        (get: 'model -> 'a,
         set: 'a -> 'msg,
         ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      TwoWayData {
        Get = get >> box
        Set = fun p _ -> p |> unbox<'a> |> set
        WrapDispatch = defaultArg wrapDispatch id
      } |> createBinding


    /// <summary>
    ///   Creates a two-way binding to an optional value. The binding
    ///   automatically converts between the optional source value and an
    ///   unwrapped (possibly <c>null</c>) value on the view side.
    /// </summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    static member twoWayOpt
        (get: 'model -> 'a option,
         set: 'a option -> 'msg,
         ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      TwoWayData {
        Get = get >> Option.map box >> Option.toObj
        Set = fun p _ -> p |> Option.ofObj |> Option.map unbox<'a> |> set
        WrapDispatch = defaultArg wrapDispatch id
      } |> createBinding


    /// <summary>
    ///   Creates a two-way binding to an optional value. The binding
    ///   automatically converts between the optional source value and an
    ///   unwrapped (possibly <c>null</c>) value on the view side.
    /// </summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    static member twoWayOpt
        (get: 'model -> 'a voption,
         set: 'a voption -> 'msg,
         ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      TwoWayData {
        Get = get >> ValueOption.map box >> ValueOption.toObj
        Set = fun p _ -> p |> ValueOption.ofObj |> ValueOption.map unbox<'a> |> set
        WrapDispatch = defaultArg wrapDispatch id
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
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    static member twoWayValidate
        (get: 'model -> 'a,
         set: 'a -> 'msg,
         validate: 'model -> string list,
         ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      TwoWayValidateData {
        Get = get >> box
        Set = fun p _ -> p |> unbox<'a> |> set
        Validate = validate
        WrapDispatch = defaultArg wrapDispatch id
      } |> createBinding


    /// <summary>
    ///   Creates a two-way binding with validation using
    ///   <c>INotifyDataErrorInfo</c>.
    /// </summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    /// <param name="validate">
    ///   Returns the validation message from the updated model.
    /// </param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    static member twoWayValidate
        (get: 'model -> 'a,
         set: 'a -> 'msg,
         validate: 'model -> string voption,
         ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      TwoWayValidateData {
        Get = get >> box
        Set = fun p _ -> p |> unbox<'a> |> set
        Validate = validate >> ValueOption.toList
        WrapDispatch = defaultArg wrapDispatch id
      } |> createBinding


    /// <summary>
    ///   Creates a two-way binding with validation using
    ///   <c>INotifyDataErrorInfo</c>.
    /// </summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    /// <param name="validate">
    ///   Returns the validation message from the updated model.
    /// </param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    static member twoWayValidate
        (get: 'model -> 'a,
         set: 'a -> 'msg,
         validate: 'model -> string option,
         ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      TwoWayValidateData {
        Get = get >> box
        Set = fun p  _ -> p |> unbox<'a> |> set
        Validate = validate >> Option.toList
        WrapDispatch = defaultArg wrapDispatch id
      } |> createBinding


    /// <summary>
    ///   Creates a two-way binding with validation using
    ///   <c>INotifyDataErrorInfo</c>.
    /// </summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    /// <param name="validate">
    ///   Returns the validation message from the updated model.
    /// </param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    static member twoWayValidate
        (get: 'model -> 'a,
         set: 'a -> 'msg,
         validate: 'model -> Result<'ignored, string>,
         ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      TwoWayValidateData {
        Get = get >> box
        Set = fun p _ -> p |> unbox<'a> |> set
        Validate = validate >> ValueOption.ofError >> ValueOption.toList
        WrapDispatch = defaultArg wrapDispatch id
      } |> createBinding


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
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    static member twoWayOptValidate
        (get: 'model -> 'a voption,
         set: 'a voption -> 'msg,
         validate: 'model -> string list,
         ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      TwoWayValidateData {
        Get = get >> ValueOption.map box >> ValueOption.toObj
        Set = fun p _ -> p |> ValueOption.ofObj |> ValueOption.map unbox<'a> |> set
        Validate = validate
        WrapDispatch = defaultArg wrapDispatch id
      } |> createBinding


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
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    static member twoWayOptValidate
        (get: 'model -> 'a voption,
         set: 'a voption -> 'msg,
         validate: 'model -> string voption,
         ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      TwoWayValidateData {
        Get = get >> ValueOption.map box >> ValueOption.toObj
        Set = fun p _ -> p |> ValueOption.ofObj |> ValueOption.map unbox<'a> |> set
        Validate = validate >> ValueOption.toList
        WrapDispatch = defaultArg wrapDispatch id
      } |> createBinding


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
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    static member twoWayOptValidate
        (get: 'model -> 'a voption,
         set: 'a voption -> 'msg,
         validate: 'model -> string option,
         ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      TwoWayValidateData {
        Get = get >> ValueOption.map box >> ValueOption.toObj
        Set = fun p _ -> p |> ValueOption.ofObj |> ValueOption.map unbox<'a> |> set
        Validate = validate >> Option.toList
        WrapDispatch = defaultArg wrapDispatch id
      } |> createBinding


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
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    static member twoWayOptValidate
        (get: 'model -> 'a voption,
         set: 'a voption -> 'msg,
         validate: 'model -> Result<'ignored, string>,
         ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      TwoWayValidateData {
        Get = get >> ValueOption.map box >> ValueOption.toObj
        Set = fun p _ -> p |> ValueOption.ofObj |> ValueOption.map unbox<'a> |> set
        Validate = validate >> ValueOption.ofError >> ValueOption.toList
        WrapDispatch = defaultArg wrapDispatch id
      } |> createBinding


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
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    static member twoWayOptValidate
        (get: 'model -> 'a option,
         set: 'a option -> 'msg,
         validate: 'model -> string list,
         ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      TwoWayValidateData {
        Get = get >> Option.map box >> Option.toObj
        Set = fun p _ -> p |> Option.ofObj |> Option.map unbox<'a> |> set
        Validate = validate
        WrapDispatch = defaultArg wrapDispatch id
      } |> createBinding


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
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    static member twoWayOptValidate
        (get: 'model -> 'a option,
         set: 'a option -> 'msg,
         validate: 'model -> string voption,
         ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      TwoWayValidateData {
        Get = get >> Option.map box >> Option.toObj
        Set = fun p _ -> p |> Option.ofObj |> Option.map unbox<'a> |> set
        Validate = validate >> ValueOption.toList
        WrapDispatch = defaultArg wrapDispatch id
      } |> createBinding


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
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    static member twoWayOptValidate
        (get: 'model -> 'a option,
         set: 'a option -> 'msg,
         validate: 'model -> string option,
         ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      TwoWayValidateData {
        Get = get >> Option.map box >> Option.toObj
        Set = fun p _ -> p |> Option.ofObj |> Option.map unbox<'a> |> set
        Validate = validate >> Option.toList
        WrapDispatch = defaultArg wrapDispatch id
      } |> createBinding


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
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    static member twoWayOptValidate
        (get: 'model -> 'a option,
         set: 'a option -> 'msg,
         validate: 'model -> Result<'ignored, string>,
         ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      TwoWayValidateData {
        Get = get >> Option.map box >> Option.toObj
        Set = fun p _ -> p |> Option.ofObj |> Option.map unbox<'a> |> set
        Validate = validate >> ValueOption.ofError >> ValueOption.toList
        WrapDispatch = defaultArg wrapDispatch id
      } |> createBinding


    /// <summary>
    ///   Creates a <c>Command</c> binding that dispatches the specified message
    ///   and can always execute.
    /// </summary>
    /// <param name="exec">Returns the message to dispatch.</param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    static member cmd
        (exec: 'msg,
         ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      CmdData {
        Exec = fun _ -> exec |> ValueSome
        CanExec = fun _ -> true
        WrapDispatch = defaultArg wrapDispatch id
      } |> createBinding


    /// <summary>
    ///   Creates a <c>Command</c> binding that dispatches the specified message
    ///   and can execute if <paramref name="canExec" /> returns <c>true</c>.
    /// </summary>
    /// <param name="exec">Returns the message to dispatch.</param>
    /// <param name="canExec">Indicates whether the command can execute.</param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    static member cmdIf
        (exec: 'msg,
         canExec: 'model -> bool,
         ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      CmdData {
        Exec = fun _ -> exec |> ValueSome
        CanExec = canExec
        WrapDispatch = defaultArg wrapDispatch id
      } |> createBinding


    /// <summary>
    ///   Creates a <c>Command</c> binding that depends on the
    ///   <c>CommandParameter</c>
    ///   and can always execute.
    /// </summary>
    /// <param name="exec">Returns the message to dispatch.</param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    static member cmdParam
        (exec: obj -> 'msg,
         ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      CmdParamData {
        Exec = fun p _ -> exec p |> ValueSome
        CanExec = fun _ _ -> true
        AutoRequery = false
        WrapDispatch = defaultArg wrapDispatch id
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
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    static member cmdParamIf
        (exec: obj -> 'msg voption,
         ?uiBoundCmdParam: bool,
         ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      CmdParamData {
        Exec = fun p _ -> exec p
        CanExec = fun p _ -> exec p |> ValueOption.isSome
        AutoRequery = defaultArg uiBoundCmdParam false
        WrapDispatch = defaultArg wrapDispatch id
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
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    static member cmdParamIf
        (exec: obj -> 'msg option,
         ?uiBoundCmdParam: bool,
         ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      CmdParamData {
        Exec = fun p _ -> exec p |> ValueOption.ofOption
        CanExec = fun p _ -> exec p |> Option.isSome
        AutoRequery = defaultArg uiBoundCmdParam false
        WrapDispatch = defaultArg wrapDispatch id
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
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    static member cmdParamIf
        (exec: obj -> Result<'msg, 'ignored>,
         ?uiBoundCmdParam: bool,
         ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      CmdParamData {
        Exec = fun p _ -> exec p |> ValueOption.ofOk
        CanExec = fun p _ -> exec p |> Result.isOk
        AutoRequery = defaultArg uiBoundCmdParam false
        WrapDispatch = defaultArg wrapDispatch id
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
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    static member cmdParamIf
        (exec: obj -> 'msg,
         canExec: obj -> bool,
         ?uiBoundCmdParam: bool,
         ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      CmdParamData {
        Exec = fun p _ -> exec p |> ValueSome
        CanExec = fun p _ -> canExec p
        AutoRequery = defaultArg uiBoundCmdParam false
        WrapDispatch = defaultArg wrapDispatch id
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
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    static member subModelSelectedItem
        (subModelSeqBindingName: string,
         get: 'model -> 'id voption,
         set: 'id voption -> 'msg,
         ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      SubModelSelectedItemData {
        Get = get >> ValueOption.map box
        Set = fun id _ -> id |> ValueOption.map unbox<'id> |> set
        SubModelSeqBindingName = subModelSeqBindingName
        WrapDispatch = defaultArg wrapDispatch id
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
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    static member subModelSelectedItem
        (subModelSeqBindingName: string,
         get: 'model -> 'id option,
         set: 'id option -> 'msg,
         ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      SubModelSelectedItemData {
        Get = get >> ValueOption.ofOption >> ValueOption.map box
        Set = fun id _ -> id |> ValueOption.map unbox<'id> |> ValueOption.toOption |> set
        SubModelSeqBindingName = subModelSeqBindingName
        WrapDispatch = defaultArg wrapDispatch id
      } |> createBinding



module BindingFn =

  open System


  [<Obsolete("Use Binding.oneWay(get)")>]
  let oneWay
      (get: 'model -> 'a)
      (name: string)
      : Binding<'model, 'msg> =
    Binding.oneWay(get) name


  [<Obsolete("Use Binding.oneWayOpt(get)")>]
  let oneWayOpt
      (get: 'model -> 'a option)
      (name: string)
      : Binding<'model, 'msg> =
    Binding.oneWayOpt(get) name


  [<Obsolete("Use Binding.oneWayLazy(get, equals, map)")>]
  let oneWayLazy
      (get: 'model -> 'a)
      (equals: 'a -> 'a -> bool)
      (map: 'a -> 'b)
      (name: string)
      : Binding<'model, 'msg> =
    Binding.oneWayLazy(get, equals, map) name


  [<Obsolete("Use Binding.oneWaySeq(get, itemEquals, getId) (note the change in parameter order of itemEquals/getId)")>]
  let oneWaySeq
      (get: 'model -> #seq<'a>)
      (getId: 'a -> 'id)
      (itemEquals: 'a -> 'a -> bool)
      (name: string)
      : Binding<'model, 'msg> =
    Binding.oneWaySeq(get, itemEquals, getId) name


  [<Obsolete("Use Binding.oneWaySeqLazy(get, equals, map, itemEquals, getId) (note the change in parameter order of itemEquals/getId)")>]
  let oneWaySeqLazy
      (get: 'model -> 'a)
      (equals: 'a -> 'a -> bool)
      (map: 'a -> #seq<'b>)
      (getId: 'b -> 'id)
      (itemEquals: 'b -> 'b -> bool)
      (name: string)
      : Binding<'model, 'msg> =
    Binding.oneWaySeqLazy(get, equals, map, itemEquals, getId) name


  [<Obsolete("Use Binding.twoWay(get, set) or another suitable overload")>]
  let twoWay
      (get: 'model -> 'a)
      (set: 'a -> 'model -> 'msg)
      (name: string)
      : Binding<'model, 'msg> =
    Binding.twoWay(get, set) name


  [<Obsolete("Use Binding.twoWayOpt(get, set) or another suitable overload")>]
  let twoWayOpt
      (get: 'model -> 'a option)
      (set: 'a option -> 'model -> 'msg)
      (name: string)
      : Binding<'model, 'msg> =
    Binding.twoWayOpt(get, set) name


  [<Obsolete("Use Binding.twoWayValidate(get, set, validate) or another suitable overload")>]
  let twoWayValidate
      (get: 'model -> 'a)
      (set: 'a -> 'model -> 'msg)
      (validate: 'model -> Result<'ignored, string>)
      (name: string)
      : Binding<'model, 'msg> =
    Binding.twoWayValidate(get, set, validate) name


  [<Obsolete("Use Binding.cmd(exec) or another suitable overload")>]
  let cmd
      (exec: 'model -> 'msg)
      (name: string)
      : Binding<'model, 'msg> =
    Binding.cmd(exec) name


  [<Obsolete("Use Binding.cmdIf(exec, canExec) or another suitable overload")>]
  let cmdIf
      (exec: 'model -> 'msg)
      (canExec: 'model -> bool)
      (name: string)
      : Binding<'model, 'msg> =
    Binding.cmdIf(exec, canExec) name


  [<Obsolete("Use Binding.cmdIf(exec) or another suitable overload")>]
  let cmdIfValid
      (exec: 'model -> Result<'msg, 'ignored>)
      (name: string)
      : Binding<'model, 'msg> =
    Binding.cmdIf(exec) name


  [<Obsolete("Use Binding.cmdParam(exec) or another suitable overload")>]
  let paramCmd
      (exec: obj -> 'model -> 'msg)
      (name: string)
      : Binding<'model, 'msg> =
    Binding.cmdParam(exec) name


  [<Obsolete("Use Binding.cmdParamIf(exec, canExec, uiTrigger) or another suitable overload")>]
  let paramCmdIf
      (exec: obj -> 'model -> 'msg)
      (canExec: obj -> 'model -> bool)
      (uiTrigger: bool)
      (name: string)
      : Binding<'model, 'msg> =
    Binding.cmdParamIf(exec, canExec, uiTrigger) name


  [<Obsolete("Use Binding.subModel(getModel, snd, toMsg, bindings) or another suitable overload")>]
  let subModel
      (getModel: 'model -> 'subModel)
      (bindings: unit -> Binding<'subModel, 'subMsg> list)
      (toMsg: 'subMsg -> 'msg)
      (name: string)
      : Binding<'model, 'msg> =
    Binding.subModel(getModel, snd, toMsg, bindings) name


  [<Obsolete("Use Binding.subModelOpt(getModel, snd, toMsg, bindings) or another suitable overload")>]
  let subModelOpt
      (getModel: 'model -> 'subModel option)
      (bindings: unit -> Binding<'subModel, 'subMsg> list)
      (toMsg: 'subMsg -> 'msg)
      (name: string)
      : Binding<'model, 'msg> =
    Binding.subModelOpt(getModel, snd, toMsg, bindings) name


  [<Obsolete("Use Binding.subModelSeq(getModels, snd, getId, toMsg, bindings) or another suitable overload")>]
  let subModelSeq
      (getModels: 'model -> #seq<'subModel>)
      (getId: 'subModel -> 'id)
      (bindings: unit -> Binding<'subModel, 'subMsg> list)
      (toMsg: 'id * 'subMsg -> 'msg)
      (name: string)
      : Binding<'model, 'msg> =
    Binding.subModelSeq(getModels, snd, getId, toMsg, bindings) name


  [<Obsolete("Use Binding.subModelSeq(getSubItems, toBindingModel, snd >> getId, snd, bindings) or another suitable overload")>]
  let subBindingSeq
      (getMainModel: 'currentModel -> 'mainModel)
      (getSubItems: 'currentModel -> #seq<'subModel>)
      (getId: 'subModel -> 'id)
      (bindings: unit -> Binding<'mainModel * 'subModel, 'msg> list)
      (name: string)
      : Binding<'currentModel, 'msg> =
    let toBindingModel (m: 'currentModel, s: 'subModel) : 'mainModel * 'subModel =
      (getMainModel m, s)
    Binding.subModelSeq(getSubItems, toBindingModel, snd >> getId, snd, bindings) name


  [<Obsolete("Use Binding.subModelSelectedItem(<c>ItemsSource</c>BindingName, get, set) or another suitable overload")>]
  let subModelSelectedItem
      (itemsSourceBindingName: string)
      (get: 'model -> 'id option)
      (set: 'id option -> 'model -> 'msg)
      (name: string)
      : Binding<'model, 'msg> =
    Binding.subModelSelectedItem(itemsSourceBindingName, get, set) name
