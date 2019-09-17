﻿namespace Elmish.WPF

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

  /// Converts None to WindowState.Closed, and Some(x) to WindowState.Visible(x).
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


type internal OneWayData<'model> = {
  Get: 'model -> obj
}

type internal OneWayLazyData<'model> = {
  Get: 'model -> obj
  Map: obj -> obj
  Equals: obj -> obj -> bool
}

type internal OneWaySeqLazyData<'model> = {
  Get: 'model -> obj
  Map: obj -> obj seq
  Equals: obj -> obj -> bool
  GetId: obj -> obj
  ItemEquals: obj -> obj -> bool
}

type internal TwoWayData<'model, 'msg> = {
  Get: 'model -> obj
  Set: obj -> 'model -> 'msg
  WrapDispatch: Dispatch<'msg> -> Dispatch<'msg>
}

type internal TwoWayValidateData<'model, 'msg> = {
  Get: 'model -> obj
  Set: obj -> 'model -> 'msg
  Validate: 'model -> string voption
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

type internal SubModelSelectedItemData<'model, 'msg> = {
  Get: 'model -> obj voption
  Set: obj voption -> 'model -> 'msg
  SubModelSeqBindingName: string
  WrapDispatch: Dispatch<'msg> -> Dispatch<'msg>
}

type internal SubModelData<'model, 'msg> = {
  GetModel: 'model -> obj voption
  GetBindings: unit -> Binding<obj, obj> list
  ToMsg: obj -> 'msg
  Sticky: bool
}

and internal SubModelWinData<'model, 'msg> = {
  GetState: 'model -> WindowState<obj>
  GetBindings: unit -> Binding<obj, obj> list
  ToMsg: obj -> 'msg
  GetWindow: unit -> Window
  IsModal: bool
  OnCloseRequested: 'msg voption
}

and internal SubModelSeqData<'model, 'msg> = {
  GetModels: 'model -> obj seq
  GetId: obj -> obj
  GetBindings: unit -> Binding<obj, obj> list
  ToMsg: obj * obj -> 'msg
}


/// Represents all necessary data used to create the different binding types.
and internal BindingData<'model, 'msg> =
  | OneWayData of OneWayData<'model>
  | OneWayLazyData of OneWayLazyData<'model>
  | OneWaySeqLazyData of OneWaySeqLazyData<'model>
  | TwoWayData of TwoWayData<'model, 'msg>
  | TwoWayValidateData of TwoWayValidateData<'model, 'msg>
  | CmdData of CmdData<'model, 'msg>
  | CmdParamData of CmdParamData<'model, 'msg>
  | SubModelData of SubModelData<'model, 'msg>
  | SubModelWinData of SubModelWinData<'model, 'msg>
  | SubModelSeqData of SubModelSeqData<'model, 'msg>
  | SubModelSelectedItemData of SubModelSelectedItemData<'model, 'msg>


/// Represents all necessary data used to create a binding.
and Binding<'model, 'msg> =
  internal
    { Name: string
      Data: BindingData<'model, 'msg> }


module internal BindingData =

  let boxDispatch (strongDispatch: Dispatch<'msg>) : Dispatch<obj> =
    unbox<'msg> >> strongDispatch

  let unboxDispatch (weakDispatch: Dispatch<obj>) : Dispatch<'msg> =
    box >> weakDispatch

  let boxWrapDispatch (strongWrapDispatch: Dispatch<'msg> -> Dispatch<'msg>) : Dispatch<obj> -> Dispatch<obj> =
    fun (weakDispatch: Dispatch<obj>) ->
      weakDispatch |> unboxDispatch |> strongWrapDispatch |> boxDispatch

  let box : BindingData<'model, 'msg> -> BindingData<obj, obj> = function
    | OneWayData d -> OneWayData {
        Get = unbox >> d.Get
      }
    | OneWayLazyData d -> OneWayLazyData {
        Get = unbox >> d.Get
        Map = d.Map
        Equals = d.Equals
      }
    | OneWaySeqLazyData d -> OneWaySeqLazyData {
        Get = unbox >> d.Get
        Map = d.Map
        Equals = d.Equals
        GetId = d.GetId
        ItemEquals = d.ItemEquals
      }
    | TwoWayData d -> TwoWayData {
        Get = unbox >> d.Get
        Set = fun v m -> d.Set v (unbox m) |> box
        WrapDispatch = boxWrapDispatch d.WrapDispatch
      }
    | TwoWayValidateData d -> TwoWayValidateData {
        Get = unbox >> d.Get
        Set = fun v m -> d.Set v (unbox m) |> box
        Validate = unbox >> d.Validate
        WrapDispatch = boxWrapDispatch d.WrapDispatch
      }
    | CmdData d -> CmdData {
        Exec = unbox >> d.Exec >> ValueOption.map box
        CanExec = unbox >> d.CanExec
        WrapDispatch = boxWrapDispatch d.WrapDispatch
      }
    | CmdParamData d -> CmdParamData {
        Exec = fun p m -> d.Exec p (unbox m) |> ValueOption.map box
        CanExec = fun p m -> d.CanExec p (unbox m)
        AutoRequery = d.AutoRequery
        WrapDispatch = boxWrapDispatch d.WrapDispatch
      }
    | SubModelData d -> SubModelData {
        GetModel = unbox >> d.GetModel
        GetBindings = d.GetBindings
        ToMsg = d.ToMsg >> unbox
        Sticky = d.Sticky
      }
    | SubModelWinData d -> SubModelWinData {
        GetState = unbox >> d.GetState
        GetBindings = d.GetBindings
        ToMsg = d.ToMsg >> unbox
        GetWindow = d.GetWindow
        IsModal = d.IsModal
        OnCloseRequested = d.OnCloseRequested |> ValueOption.map box
      }
    | SubModelSeqData d -> SubModelSeqData {
        GetModels = unbox >> d.GetModels
        GetId = d.GetId
        GetBindings = d.GetBindings
        ToMsg = d.ToMsg >> unbox
      }
    | SubModelSelectedItemData d -> SubModelSelectedItemData {
        Get = unbox >> d.Get
        Set = fun v m -> d.Set v (unbox m) |> box
        SubModelSeqBindingName = d.SubModelSeqBindingName
        WrapDispatch = boxWrapDispatch d.WrapDispatch
      }



[<AutoOpen>]
module internal Helpers =

  let createBinding data name =
    { Name = name
      Data = data }

  let boxBinding binding =
    { Name = binding.Name
      Data = BindingData.box binding.Data }



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
  ///   Creates a one-way binding to an optional value. The binding automatically
  ///   converts between the optional source value and an unwrapped (possibly
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
  ///   Creates a one-way binding to an optional value. The binding automatically
  ///   converts between the optional source value and an unwrapped (possibly
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
  //    will be called only when the output of <paramref name="get" /> changes,
  ///   as determined by <paramref name="equals" />. This may have better
  ///   performance than <see cref="oneWay" /> for expensive computations
  ///   (but may be less performant for non-expensive functions due to additional overhead).
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
  ///   Creates a lazily evaluated one-way binding to an optional value. The binding
  ///   automatically converts between the optional source value and an unwrapped
  ///   (possibly <c>null</c>) value on the view side. <paramref name="map" /> will be
  ///   called only when the output of <paramref name="get" /> changes, as determined
  ///   by <paramref name="equals" />.
  ///
  ///   This may have better performance than a non-lazy binding for expensive
  ///   computations (but may be less performant for non-expensive functions due to
  ///   additional overhead).
  /// </summary>
  /// <param name="get">Gets the intermediate value from the model.</param>
  /// <param name="equals">
  ///   Indicates whether two intermediate values are equal. Good candidates are
  ///   <c>elmEq</c> and <c>refEq</c>.
  /// </param>
  /// <param name="map">Transforms the intermediate value into the final type.</param>
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
  ///   Creates a lazily evaluated one-way binding to an optional value. The binding
  ///   automatically converts between the optional source value and an unwrapped
  ///   (possibly <c>null</c>) value on the view side. <paramref name="map" /> will be
  ///   called only when the output of <paramref name="get" /> changes, as determined
  ///   by <paramref name="equals" />.
  ///
  ///   This may have better performance than a non-lazy binding for expensive
  ///   computations (but may be less performant for non-expensive functions due to
  ///   additional overhead).
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="equals">
  ///   Indicates whether two intermediate values are equal. Good candidates are
  ///   <c>elmEq</c> and <c>refEq</c>.
  /// </param>
  /// <param name="map">Transforms the intermediate value into the final type.</param>
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
  ///   Creates a one-way binding to a sequence of items, each uniquely identified
  ///   by the value returned by <paramref name="getId"/>. The binding is backed
  ///   by a persistent <c>ObservableCollection</c>, so only changed items (as determined
  ///   by <paramref name="itemEquals" />) will be replaced. If the items are complex
  ///   and you want them updated instead of replaced, consider using <see cref="subModelSeq" />.
  /// </summary>
  /// <param name="get">Gets the collection from the model.</param>
  /// <param name="itemEquals">
  ///   Indicates whether two collection items are equal. Good candidates are
  ///   <c>elmEq</c>, <c>refEq</c>, or simply <c>(=)</c>.
  /// </param>
  /// <param name="getId">Gets a unique identifier for a collection item.</param>
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
  ///   Creates a one-way binding to a sequence of items, each uniquely identified
  ///   by the value returned by <paramref name="getId"/>. The binding will only
  ///   be updated if the output of <paramref name="get" /> changes, as determined
  ///   by <paramref name="equals" />. The binding is backed by a persistent
  ///   <c>ObservableCollection</c>, so only changed items (as determined by
  ///   <paramref name="itemEquals" />) will be replaced. If the items are complex
  ///   and you want them updated instead of replaced, consider using <see cref="subModelSeq" />.
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
  /// <param name="getId">Gets a unique identifier for a collection item.</param>
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
  ///   Wraps the dispatch function with additional behavior,
  ///   such as throttling, debouncing, or limiting.
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
  ///   Creates a two-way binding to an optional value. The binding automatically
  ///   converts between the optional source value and an unwrapped (possibly <c>null</c>)
  ///   value on the view side.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior,
  ///   such as throttling, debouncing, or limiting.
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
  ///   Creates a two-way binding to an optional value. The binding automatically
  ///   converts between the optional source value and an unwrapped (possibly <c>null</c>)
  ///   value on the view side.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior,
  ///   such as throttling, debouncing, or limiting.
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
  ///   Creates a two-way binding with validation using <c>INotifyDataErrorInfo</c>.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="validate">
  ///   Returns the validation message from the updated model.
  /// </param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior,
  ///   such as throttling, debouncing, or limiting.
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
      Validate = validate
      WrapDispatch = defaultArg wrapDispatch id
    } |> createBinding


  /// <summary>
  ///   Creates a two-way binding with validation using <c>INotifyDataErrorInfo</c>.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="validate">
  ///   Returns the validation message from the updated model.
  /// </param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior,
  ///   such as throttling, debouncing, or limiting.
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
      Validate = validate >> ValueOption.ofOption
      WrapDispatch = defaultArg wrapDispatch id
    } |> createBinding


  /// <summary>
  ///   Creates a two-way binding with validation using <c>INotifyDataErrorInfo</c>.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="validate">
  ///   Returns the validation message from the updated model.
  /// </param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior,
  ///   such as throttling, debouncing, or limiting.
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
      Validate = validate >> ValueOption.ofError
      WrapDispatch = defaultArg wrapDispatch id
    } |> createBinding


  /// <summary>
  ///   Creates a two-way binding to an optional value with validation using
  ///   <c>INotifyDataErrorInfo</c>. The binding automatically converts between the
  ///   optional source value and an unwrapped (possibly <c>null</c>) value on the
  ///   view side.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="validate">
  ///   Returns the validation message from the updated model.
  /// </param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior,
  ///   such as throttling, debouncing, or limiting.
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
      Validate = validate
      WrapDispatch = defaultArg wrapDispatch id
    } |> createBinding


  /// <summary>
  ///   Creates a two-way binding to an optional value with validation using
  ///   <c>INotifyDataErrorInfo</c>. The binding automatically converts between the
  ///   optional source value and an unwrapped (possibly <c>null</c>) value on the
  ///   view side.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="validate">
  ///   Returns the validation message from the updated model.
  /// </param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior,
  ///   such as throttling, debouncing, or limiting.
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
      Validate = validate >> ValueOption.ofOption
      WrapDispatch = defaultArg wrapDispatch id
    } |> createBinding


  /// <summary>
  ///   Creates a two-way binding to an optional value with validation using
  ///   <c>INotifyDataErrorInfo</c>. The binding automatically converts between the
  ///   optional source value and an unwrapped (possibly <c>null</c>) value on the
  ///   view side.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="validate">
  ///   Returns the validation message from the updated model.
  /// </param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior,
  ///   such as throttling, debouncing, or limiting.
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
      Validate = validate >> ValueOption.ofError
      WrapDispatch = defaultArg wrapDispatch id
    } |> createBinding


  /// <summary>
  ///   Creates a two-way binding to an optional value with validation using
  ///   <c>INotifyDataErrorInfo</c>. The binding automatically converts between the
  ///   optional source value and an unwrapped (possibly <c>null</c>) value on the
  ///   view side.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="validate">
  ///   Returns the validation message from the updated model.
  /// </param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior,
  ///   such as throttling, debouncing, or limiting.
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
      Validate = validate
      WrapDispatch = defaultArg wrapDispatch id
    } |> createBinding


  /// <summary>
  ///   Creates a two-way binding to an optional value with validation using
  ///   <c>INotifyDataErrorInfo</c>. The binding automatically converts between the
  ///   optional source value and an unwrapped (possibly <c>null</c>) value on the
  ///   view side.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="validate">
  ///   Returns the validation message from the updated model.
  /// </param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior,
  ///   such as throttling, debouncing, or limiting.
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
      Validate = validate >> ValueOption.ofOption
      WrapDispatch = defaultArg wrapDispatch id
    } |> createBinding


  /// <summary>
  ///   Creates a two-way binding to an optional value with validation using
  ///   <c>INotifyDataErrorInfo</c>. The binding automatically converts between the
  ///   optional source value and an unwrapped (possibly <c>null</c>) value on the
  ///   view side.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="validate">
  ///   Returns the validation message from the updated model.
  /// </param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior,
  ///   such as throttling, debouncing, or limiting.
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
      Validate = validate >> ValueOption.ofError
      WrapDispatch = defaultArg wrapDispatch id
    } |> createBinding


  /// <summary>
  ///   Creates a <c>Command</c> binding that depends only on the model (not the
  ///   <c>CommandParameter</c>) and can always execute.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior,
  ///   such as throttling, debouncing, or limiting.
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
  ///   Creates a conditional <c>Command</c> binding that depends only on the model
  ///   (not the <c>CommandParameter</c>) and can execute if <paramref name="canExec" />
  ///   returns <c>true</c>.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  /// <param name="canExec">Indicates whether the command can execute.</param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior,
  ///   such as throttling, debouncing, or limiting.
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
  ///   Creates a conditional <c>Command</c> binding that depends only on the model
  ///   (not the <c>CommandParameter</c>) and can execute if <paramref name="exec" />
  ///   returns <c>ValueSome</c>.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior,
  ///   such as throttling, debouncing, or limiting.
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
  ///   Creates a conditional <c>Command</c> binding that depends only on the model
  ///   (not the <c>CommandParameter</c>) and can execute if <paramref name="exec" />
  ///   returns <c>Some</c>.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior,
  ///   such as throttling, debouncing, or limiting.
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
  ///   Creates a conditional <c>Command</c> binding that depends only on the model
  ///   (not the <c>CommandParameter</c>) and can execute if <paramref name="exec" />
  ///   returns <c>Ok</c>.
  ///
  ///   This overload allows more easily re-using the same validation functions
  ///   for inputs and commands.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior,
  ///   such as throttling, debouncing, or limiting.
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
  ///   Creates a <c>Command</c> binding that depends on the <c>CommandParameter</c>
  ///   and can always execute.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior,
  ///   such as throttling, debouncing, or limiting.
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
  ///   Creates a <c>Command</c> binding that depends on the <c>CommandParameter</c>
  ///   and can execute if <paramref name="canExec" /> returns <c>true</c>.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  /// <param name="canExec">Indicates whether the command can execute.</param>
  /// <param name="uiBoundCmdParam">
  ///   If <c>true</c>, <c>CanExecuteChanged</c> will trigger every time WPF's <c>CommandManager</c>
  ///   detects UI changes that could potentially influence the command's ability
  ///   to execute. This will likely lead to many more triggers than necessary,
  ///   but is needed if you have bound the <c>CommandParameter</c> to another UI property.
  /// </param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior,
  ///   such as throttling, debouncing, or limiting.
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
  ///   Creates a conditional <c>Command</c> binding that depends on the <c>CommandParameter</c>
  ///   and can execute if <paramref name="exec" /> returns <c>ValueSome</c>.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  /// <param name="uiBoundCmdParam">
  ///   If <c>true</c>, <c>CanExecuteChanged</c> will trigger every time WPF's <c>CommandManager</c>
  ///   detects UI changes that could potentially influence the command's ability
  ///   to execute. This will likely lead to many more triggers than necessary,
  ///   but is needed if you have bound the <c>CommandParameter</c> to another UI property.
  /// </param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior,
  ///   such as throttling, debouncing, or limiting.
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
  ///   Creates a conditional <c>Command</c> binding that depends on the <c>CommandParameter</c>
  ///   and can execute if <paramref name="exec" /> returns <c>Some</c>.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  /// <param name="uiBoundCmdParam">
  ///   If <c>true</c>, <c>CanExecuteChanged</c> will trigger every time WPF's <c>CommandManager</c>
  ///   detects UI changes that could potentially influence the command's ability
  ///   to execute. This will likely lead to many more triggers than necessary,
  ///   but is needed if you have bound the <c>CommandParameter</c> to another UI property.
  /// </param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior,
  ///   such as throttling, debouncing, or limiting.
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
  ///   Creates a conditional <c>Command</c> binding that depends on the <c>CommandParameter</c>
  ///   and can execute if <paramref name="exec" /> returns <c>Ok</c>.
  ///
  ///   This overload allows more easily re-using the same validation functions
  ///   for inputs and commands.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  /// <param name="uiBoundCmdParam">
  ///   If <c>true</c>, <c>CanExecuteChanged</c> will trigger every time WPF's <c>CommandManager</c>
  ///   detects UI changes that could potentially influence the command's ability
  ///   to execute. This will likely lead to many more triggers than necessary,
  ///   but is needed if you have bound the <c>CommandParameter</c> to another UI property.
  /// </param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior,
  ///   such as throttling, debouncing, or limiting.
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
  ///   You typically bind this to the <c>DataContext</c> of a <c>UserControl</c> or similar.
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
  ///   model will return <c>null</c> unless <paramref name="sticky" /> is <c>true</c>,
  ///   in which case the last non-<c>null</c> model will be returned. You typically bind this to
  ///   the <c>DataContext</c> of a <c>UserControl</c> or similar.
  ///
  ///   The 'sticky' part is useful if you want to e.g. animate away a <c>UserControl</c> when
  ///   the model is missing, but don't want the data used by that control to be
  ///   cleared once the animation starts. (The animation must be triggered using another
  ///   binding since this will never return <c>null</c>.)
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
  ///   If <c>true</c>, when the model is missing, the last non-<c>null</c> model will be returned
  ///   instead of <c>null</c>.
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
  ///   model will return <c>null</c> unless <paramref name="sticky" /> is <c>true</c>, in which
  ///   case the last non-<c>null</c> model will be returned. You typically bind this to
  ///   the <c>DataContext</c> of a <c>UserControl</c> or similar.
  ///
  ///   The 'sticky' part is useful if you want to e.g. animate away a <c>UserControl</c> when
  ///   the model is missing, but don't want the data used by that control to be
  ///   cleared once the animation starts. (The animation must be triggered using another
  ///   binding since this will never return <c>null</c>.)
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
  ///   If <c>true</c>, when the model is missing, the last non-<c>null</c> model will be returned
  ///   instead of <c>null</c>.
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
  ///   model will return <c>null</c> unless <paramref name="sticky" /> is <c>true</c>, in which
  ///   case the last non-<c>null</c> model will be returned. You typically bind this to
  ///   the <c>DataContext</c> of a <c>UserControl</c> or similar.
  ///
  ///   The 'sticky' part is useful if you want to e.g. animate away a <c>UserControl</c> when
  ///   the model is missing, but don't want the data used by that control to be
  ///   cleared once the animation starts. (The animation must be triggered using another
  ///   binding since this will never return <c>null</c>.)
  /// </summary>
  /// <param name="getSubModel">Gets the sub-model from the model.</param>
  /// <param name="toMsg">
  ///   Converts the messages used in the bindings to parent model messages
  ///   (e.g. a parent message union case that wraps the child message type).
  /// </param>
  /// <param name="bindings">Returns the bindings for the sub-model.</param>
  /// <param name="sticky">
  ///   If <c>true</c>, when the model is missing, the last non-<c>null</c> model will be returned
  ///   instead of <c>null</c>.
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
  ///   model will return <c>null</c> unless <paramref name="sticky" /> is <c>true</c>, in which
  ///   case the last non-<c>null</c> model will be returned. You typically bind this to
  ///   the <c>DataContext</c> of a <c>UserControl</c> or similar.
  ///
  ///   The 'sticky' part is useful if you want to e.g. animate away a <c>UserControl</c> when
  ///   the model is missing, but don't want the data used by that control to be
  ///   cleared once the animation starts. (The animation must be triggered using another
  ///   binding since this will never return <c>null</c>.)
  /// </summary>
  /// <param name="getSubModel">Gets the sub-model from the model.</param>
  /// <param name="toMsg">
  ///   Converts the messages used in the bindings to parent model messages
  ///   (e.g. a parent message union case that wraps the child message type).
  /// </param>
  /// <param name="bindings">Returns the bindings for the sub-model.</param>
  /// <param name="sticky">
  ///   If <c>true</c>, when the model is missing, the last non-<c>null</c> model will be returned
  ///   instead of <c>null</c>.
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
  ///   return <c>null</c> unless <paramref name="sticky" /> is <c>true</c>, in which case
  ///   the last non-<c>null</c> model will be returned. You typically bind this to
  ///   the <c>DataContext</c> of a <c>UserControl</c> or similar.
  ///
  ///   The 'sticky' part is useful if you want to e.g. animate away a <c>UserControl</c> when
  ///   the model is missing, but don't want the data used by that control to be
  ///   cleared once the animation starts. (The animation must be triggered using another
  ///   binding since this will never return <c>null</c>.)
  /// </summary>
  /// <param name="getSubModel">Gets the sub-model from the model.</param>
  /// <param name="bindings">Returns the bindings for the sub-model.</param>
  /// <param name="sticky">
  ///   If <c>true</c>, when the model is missing, the last non-<c>null</c> model will be returned
  ///   instead of <c>null</c>.
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
  ///   return <c>null</c> unless <paramref name="sticky" /> is <c>true</c>, in which case
  ///   the last non-<c>null</c> model will be returned. You typically bind this to
  ///   the <c>DataContext</c> of a <c>UserControl</c> or similar.
  ///
  ///   The 'sticky' part is useful if you want to e.g. animate away a <c>UserControl</c> when
  ///   the model is missing, but don't want the data used by that control to be
  ///   cleared once the animation starts. (The animation must be triggered using another
  ///   binding since this will never return <c>null</c>.)
  /// </summary>
  /// <param name="getSubModel">Gets the sub-model from the model.</param>
  /// <param name="bindings">Returns the bindings for the sub-model.</param>
  /// <param name="sticky">
  ///   If <c>true</c>, when the model is missing, the last non-<c>null</c> model will be returned
  ///   instead of <c>null</c>.
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
  ///   <paramref name="onCloseRequested" /> to be dispatched. You should
  ///   supply <paramref name="onCloseRequested" /> and react to this in a
  ///   manner that will not confuse a user trying to close the window (e.g. by
  ///   closing it, or displaying relevant feedback to the user.)
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
  ///   The function used to get the window. Can be a simple window constructor,
  ///   or a function that also configures the window (setting owner etc.).
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
    SubModelWinData {
      GetState = fun m ->
        getState m |> WindowState.map (fun sub -> toBindingModel (m, sub) |> box)
      GetBindings = bindings >> List.map boxBinding
      ToMsg = unbox<'bindingMsg> >> toMsg
      GetWindow = fun () -> upcast getWindow ()
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
  ///   <paramref name="onCloseRequested" /> to be dispatched. You should
  ///   supply <paramref name="onCloseRequested" /> and react to this in a
  ///   manner that will not confuse a user trying to close the window (e.g. by
  ///   closing it, or displaying relevant feedback to the user.)
  /// </summary>
  /// <param name="getState">Gets the window state and a sub-model.</param>
  /// <param name="toMsg">
  ///   Converts the messages used in the bindings to parent model messages
  ///   (e.g. a parent message union case that wraps the child message type).
  /// </param>
  /// <param name="bindings">Returns the bindings for the sub-model.</param>
  /// <param name="getWindow">
  ///   The function used to get the window. Can be a simple window constructor,
  ///   or a function that also configures the window (setting owner etc.).
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
    SubModelWinData {
      GetState = fun m ->
        getState m |> WindowState.map (fun sub -> (m, sub) |> box)
      GetBindings = bindings >> List.map boxBinding
      ToMsg = unbox<'subMsg> >> toMsg
      GetWindow = fun () -> upcast getWindow ()
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
  ///   <paramref name="onCloseRequested" /> to be dispatched. You should
  ///   supply <paramref name="onCloseRequested" /> and react to this in a
  ///   manner that will not confuse a user trying to close the window (e.g. by
  ///   closing it, or displaying relevant feedback to the user.)
  /// </summary>
  /// <param name="getState">Gets the window state and a sub-model.</param>
  /// <param name="bindings">Returns the bindings for the sub-model.</param>
  /// <param name="getWindow">
  ///   The function used to get the window. Can be a simple window constructor,
  ///   or a function that also configures the window (setting owner etc.).
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
    SubModelWinData {
      GetState = fun m ->
        getState m |> WindowState.map (fun sub -> (m, sub) |> box)
      GetBindings = bindings >> List.map boxBinding
      ToMsg = unbox<'msg>
      GetWindow = fun () -> upcast getWindow ()
      IsModal = defaultArg isModal false
      OnCloseRequested = defaultArg (onCloseRequested |> Option.map ValueSome) ValueNone
    } |> createBinding


  /// <summary>
  ///   Creates a binding to a sequence of sub-models, each uniquely identified
  ///   by the value returned by <paramref name="getId" />. The sub-models have
  ///   their own bindings and message type. You typically bind this to the
  ///   <c>ItemsSource</c> of an <c>ItemsControl</c>, <c>ListView</c>, <c>TreeView</c>, etc.
  /// </summary>
  /// <param name="getSubModels">Gets the sub-models from the model.</param>
  /// <param name="toBindingModel">
  ///   Converts the models to the model used by the bindings.
  /// </param>
  /// <param name="getId">Gets a unique identifier for a sub-model.</param>
  /// <param name="toMsg">
  ///   Converts the sub-model ID and messages used in the bindings to parent
  ///   model messages (e.g. a parent message union case that wraps the sub-model
  ///   ID and message type).
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
  ///   <c>ItemsSource</c> of an <c>ItemsControl</c>, <c>ListView</c>, <c>TreeView</c>, etc.
  /// </summary>
  /// <param name="getSubModels">Gets the sub-models from the model.</param>
  /// <param name="getId">Gets a unique identifier for a sub-model.</param>
  /// <param name="toMsg">
  ///   Converts the sub-model ID and messages used in the bindings to parent
  ///   model messages (e.g. a parent message union case that wraps the sub-model
  ///   ID and message type).
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
  ///   their own bindings. You typically bind this to the <c>ItemsSource</c> of an
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
  ///   Creates a two-way binding to a <c>SelectedItem</c>-like property where the
  ///   <c>ItemsSource</c>-like property is a <see cref="subModelSeq" /> binding.
  ///   Automatically converts the dynamically created Elmish.WPF view models to/from
  ///   their corresponding IDs, so the Elmish user code only has to work with the IDs.
  ///
  ///   Only use this if you are unable to use some kind of <c>SelectedValue</c> or
  ///   <c>SelectedIndex</c> property with a normal <see cref="twoWay" /> binding.
  ///   This binding is less type-safe and will throw at runtime if
  ///   <paramref name="subModelSeqBindingName" /> does not correspond to a
  ///   <see cref="subModelSeq" /> binding, or if the inferred <c>'id</c> type
  ///   does not match the actual ID type used in that binding.
  /// </summary>
  /// <param name="subModelSeqBindingName">
  ///   The name of the <see cref="subModelSeq" /> binding used as the items source.
  /// </param>
  /// <param name="get">Gets the selected sub-model/sub-binding ID from the model.</param>
  /// <param name="set">
  ///   Returns the message to dispatch on selections/de-selections.
  /// </param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior,
  ///   such as throttling, debouncing, or limiting.
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
  ///   Creates a two-way binding to a <c>SelectedItem</c>-like property where the
  ///   <c>ItemsSource</c>-like property is a <see cref="subModelSeq" /> binding.
  ///   Automatically converts the dynamically created Elmish.WPF view models to/from
  ///   their corresponding IDs, so the Elmish user code only has to work with the IDs.
  ///
  ///   Only use this if you are unable to use some kind of <c>SelectedValue</c> or
  ///   <c>SelectedIndex</c> property with a normal <see cref="twoWay" /> binding.
  ///   This binding is less type-safe and will throw at runtime if
  ///   <paramref name="subModelSeqBindingName" /> does not correspond to a
  ///   <see cref="subModelSeq" /> binding, or if the inferred <c>'id</c> type
  ///   does not match the actual ID type used in that binding.
  /// </summary>
  /// <param name="subModelSeqBindingName">
  ///   The name of the <see cref="subModelSeq" /> binding used as the items source.
  /// </param>
  /// <param name="get">Gets the selected sub-model/sub-binding ID from the model.</param>
  /// <param name="set">
  ///   Returns the message to dispatch on selections/de-selections.
  /// </param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior,
  ///   such as throttling, debouncing, or limiting.
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
    ///   Wraps the dispatch function with additional behavior,
    ///   such as throttling, debouncing, or limiting.
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
    ///   Creates a two-way binding to an optional value. The binding automatically
    ///   converts between the optional source value and an unwrapped (possibly <c>null</c>)
    ///   value on the view side.
    /// </summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior,
    ///   such as throttling, debouncing, or limiting.
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
    ///   Creates a two-way binding to an optional value. The binding automatically
    ///   converts between the optional source value and an unwrapped (possibly <c>null</c>)
    ///   value on the view side.
    /// </summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior,
    ///   such as throttling, debouncing, or limiting.
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
    ///   Creates a two-way binding with validation using <c>INotifyDataErrorInfo</c>.
    /// </summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    /// <param name="validate">
    ///   Returns the validation message from the updated model.
    /// </param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior,
    ///   such as throttling, debouncing, or limiting.
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
        Validate = validate
        WrapDispatch = defaultArg wrapDispatch id
      } |> createBinding


    /// <summary>
    ///   Creates a two-way binding with validation using <c>INotifyDataErrorInfo</c>.
    /// </summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    /// <param name="validate">
    ///   Returns the validation message from the updated model.
    /// </param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior,
    ///   such as throttling, debouncing, or limiting.
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
        Validate = validate >> ValueOption.ofOption
        WrapDispatch = defaultArg wrapDispatch id
      } |> createBinding


    /// <summary>
    ///   Creates a two-way binding with validation using <c>INotifyDataErrorInfo</c>.
    /// </summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    /// <param name="validate">
    ///   Returns the validation message from the updated model.
    /// </param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior,
    ///   such as throttling, debouncing, or limiting.
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
        Validate = validate >> ValueOption.ofError
        WrapDispatch = defaultArg wrapDispatch id
      } |> createBinding


    /// <summary>
    ///   Creates a two-way binding to an optional value with validation using
    ///   <c>INotifyDataErrorInfo</c>. The binding automatically converts between the
    ///   optional source value and an unwrapped (possibly <c>null</c>) value on the
    ///   view side.
    /// </summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    /// <param name="validate">
    ///   Returns the validation message from the updated model.
    /// </param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior,
    ///   such as throttling, debouncing, or limiting.
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
        Validate = validate
        WrapDispatch = defaultArg wrapDispatch id
      } |> createBinding


    /// <summary>
    ///   Creates a two-way binding to an optional value with validation using
    ///   <c>INotifyDataErrorInfo</c>. The binding automatically converts between the
    ///   optional source value and an unwrapped (possibly <c>null</c>) value on the
    ///   view side.
    /// </summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    /// <param name="validate">
    ///   Returns the validation message from the updated model.
    /// </param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior,
    ///   such as throttling, debouncing, or limiting.
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
        Validate = validate >> ValueOption.ofOption
        WrapDispatch = defaultArg wrapDispatch id
      } |> createBinding


    /// <summary>
    ///   Creates a two-way binding to an optional value with validation using
    ///   <c>INotifyDataErrorInfo</c>. The binding automatically converts between the
    ///   optional source value and an unwrapped (possibly <c>null</c>) value on the
    ///   view side.
    /// </summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    /// <param name="validate">
    ///   Returns the validation message from the updated model.
    /// </param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior,
    ///   such as throttling, debouncing, or limiting.
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
        Validate = validate >> ValueOption.ofError
        WrapDispatch = defaultArg wrapDispatch id
      } |> createBinding


    /// <summary>
    ///   Creates a two-way binding to an optional value with validation using
    ///   <c>INotifyDataErrorInfo</c>. The binding automatically converts between the
    ///   optional source value and an unwrapped (possibly <c>null</c>) value on the
    ///   view side.
    /// </summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    /// <param name="validate">
    ///   Returns the validation message from the updated model.
    /// </param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior,
    ///   such as throttling, debouncing, or limiting.
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
        Validate = validate
        WrapDispatch = defaultArg wrapDispatch id
      } |> createBinding


    /// <summary>
    ///   Creates a two-way binding to an optional value with validation using
    ///   <c>INotifyDataErrorInfo</c>. The binding automatically converts between the
    ///   optional source value and an unwrapped (possibly <c>null</c>) value on the
    ///   view side.
    /// </summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    /// <param name="validate">
    ///   Returns the validation message from the updated model.
    /// </param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior,
    ///   such as throttling, debouncing, or limiting.
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
        Validate = validate >> ValueOption.ofOption
        WrapDispatch = defaultArg wrapDispatch id
      } |> createBinding


    /// <summary>
    ///   Creates a two-way binding to an optional value with validation using
    ///   <c>INotifyDataErrorInfo</c>. The binding automatically converts between the
    ///   optional source value and an unwrapped (possibly <c>null</c>) value on the
    ///   view side.
    /// </summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    /// <param name="validate">
    ///   Returns the validation message from the updated model.
    /// </param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior,
    ///   such as throttling, debouncing, or limiting.
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
        Validate = validate >> ValueOption.ofError
        WrapDispatch = defaultArg wrapDispatch id
      } |> createBinding


    /// <summary>
    ///   Creates a <c>Command</c> binding that dispatches the specified message
    ///   and can always execute.
    /// </summary>
    /// <param name="exec">Returns the message to dispatch.</param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior,
    ///   such as throttling, debouncing, or limiting.
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
    ///   Creates a <c>Command</c> binding that dispatches the specified message and
    ///   can execute if <paramref name="canExec" /> returns <c>true</c>.
    /// </summary>
    /// <param name="exec">Returns the message to dispatch.</param>
    /// <param name="canExec">Indicates whether the command can execute.</param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior,
    ///   such as throttling, debouncing, or limiting.
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
    ///   Creates a <c>Command</c> binding that depends on the <c>CommandParameter</c>
    ///   and can always execute.
    /// </summary>
    /// <param name="exec">Returns the message to dispatch.</param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior,
    ///   such as throttling, debouncing, or limiting.
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
    ///   Creates a conditional <c>Command</c> binding that depends on the <c>CommandParameter</c>
    ///   and can execute if <paramref name="exec" /> returns <c>ValueSome</c>.
    /// </summary>
    /// <param name="exec">Returns the message to dispatch.</param>
    /// <param name="uiBoundCmdParam">
    ///   If <c>true</c>, <c>CanExecuteChanged</c> will trigger every time WPF's <c>CommandManager</c>
    ///   detects UI changes that could potentially influence the command's ability
    ///   to execute. This will likely lead to many more triggers than necessary,
    ///   but is needed if you have bound the <c>CommandParameter</c> to another UI property.
    /// </param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior,
    ///   such as throttling, debouncing, or limiting.
    /// </param>
    static member cmdParamIf
        (exec: obj -> 'msg voption,
         ?uiBoundCmdParam: bool,
         ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      CmdParamData {
        Exec = fun p m -> exec p
        CanExec = fun p m -> exec p |> ValueOption.isSome
        AutoRequery = defaultArg uiBoundCmdParam false
        WrapDispatch = defaultArg wrapDispatch id
      } |> createBinding


    /// <summary>
    ///   Creates a conditional <c>Command</c> binding that depends on the <c>CommandParameter</c>
    ///   and can execute if <paramref name="exec" /> returns <c>Some</c>.
    /// </summary>
    /// <param name="exec">Returns the message to dispatch.</param>
    /// <param name="uiBoundCmdParam">
    ///   If <c>true</c>, <c>CanExecuteChanged</c> will trigger every time WPF's <c>CommandManager</c>
    ///   detects UI changes that could potentially influence the command's ability
    ///   to execute. This will likely lead to many more triggers than necessary,
    ///   but is needed if you have bound the <c>CommandParameter</c> to another UI property.
    /// </param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior,
    ///   such as throttling, debouncing, or limiting.
    /// </param>
    static member cmdParamIf
        (exec: obj -> 'msg option,
         ?uiBoundCmdParam: bool,
         ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      CmdParamData {
        Exec = fun p m -> exec p |> ValueOption.ofOption
        CanExec = fun p m -> exec p |> Option.isSome
        AutoRequery = defaultArg uiBoundCmdParam false
        WrapDispatch = defaultArg wrapDispatch id
      } |> createBinding


    /// <summary>
    ///   Creates a conditional <c>Command</c> binding that depends on the <c>CommandParameter</c>
    ///   and can execute if <paramref name="exec" /> returns <c>Ok</c>.
    ///
    ///   This overload allows more easily re-using the same validation functions
    ///   for inputs and commands.
    /// </summary>
    /// <param name="exec">Returns the message to dispatch.</param>
    /// <param name="uiBoundCmdParam">
    ///   If <c>true</c>, <c>CanExecuteChanged</c> will trigger every time WPF's <c>CommandManager</c>
    ///   detects UI changes that could potentially influence the command's ability
    ///   to execute. This will likely lead to many more triggers than necessary,
    ///   but is needed if you have bound the <c>CommandParameter</c> to another UI property.
    /// </param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior,
    ///   such as throttling, debouncing, or limiting.
    /// </param>
    static member cmdParamIf
        (exec: obj -> Result<'msg, 'ignored>,
         ?uiBoundCmdParam: bool,
         ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      CmdParamData {
        Exec = fun p m -> exec p |> ValueOption.ofOk
        CanExec = fun p m -> exec p |> Result.isOk
        AutoRequery = defaultArg uiBoundCmdParam false
        WrapDispatch = defaultArg wrapDispatch id
      } |> createBinding


    /// <summary>
    ///   Creates a <c>Command</c> binding that depends on the <c>CommandParameter</c>
    ///   and can execute if <paramref name="canExec" /> returns <c>true</c>.
    /// </summary>
    /// <param name="exec">Returns the message to dispatch.</param>
    /// <param name="canExec">Indicates whether the command can execute.</param>
    /// <param name="uiBoundCmdParam">
    ///   If <c>true</c>, <c>CanExecuteChanged</c> will trigger every time WPF's <c>CommandManager</c>
    ///   detects UI changes that could potentially influence the command's ability
    ///   to execute. This will likely lead to many more triggers than necessary,
    ///   but is needed if you have bound the <c>CommandParameter</c> to another UI property.
    /// </param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior,
    ///   such as throttling, debouncing, or limiting.
    /// </param>
    static member cmdParamIf
        (exec: obj -> 'msg,
         canExec: obj -> bool,
         ?uiBoundCmdParam: bool,
         ?wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      CmdParamData {
        Exec = fun p m -> exec p |> ValueSome
        CanExec = fun p m -> canExec p
        AutoRequery = defaultArg uiBoundCmdParam false
        WrapDispatch = defaultArg wrapDispatch id
      } |> createBinding


    /// <summary>
    ///   Creates a two-way binding to a <c>SelectedItem</c>-like property where the
    ///   <c>ItemsSource</c>-like property is a <see cref="subModelSeq" /> binding.
    ///   Automatically converts the dynamically created Elmish.WPF view models to/from
    ///   their corresponding IDs, so the Elmish user code only has to work with the IDs.
    ///
    ///   Only use this if you are unable to use some kind of <c>SelectedValue</c> or
    ///   <c>SelectedIndex</c> property with a normal <see cref="twoWay" /> binding.
    ///   This binding is less type-safe and will throw at runtime if
    ///   <paramref name="subModelSeqBindingName" /> does not correspond to a
    ///   <see cref="subModelSeq" /> binding, or if the inferred <c>'id</c> type
    ///   does not match the actual ID type used in that binding.
    /// </summary>
    /// <param name="subModelSeqBindingName">
    ///   The name of the <see cref="subModelSeq" /> binding used as the items source.
    /// </param>
    /// <param name="get">Gets the selected sub-model/sub-binding ID from the model.</param>
    /// <param name="set">
    ///   Returns the message to dispatch on selections/de-selections.
    /// </param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior,
    ///   such as throttling, debouncing, or limiting.
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
    ///   Creates a two-way binding to a <c>SelectedItem</c>-like property where the
    ///   <c>ItemsSource</c>-like property is a <see cref="subModelSeq" /> binding.
    ///   Automatically converts the dynamically created Elmish.WPF view models to/from
    ///   their corresponding IDs, so the Elmish user code only has to work with the IDs.
    ///
    ///   Only use this if you are unable to use some kind of <c>SelectedValue</c> or
    ///   <c>SelectedIndex</c> property with a normal <see cref="twoWay" /> binding.
    ///   This binding is less type-safe and will throw at runtime if
    ///   <paramref name="subModelSeqBindingName" /> does not correspond to a
    ///   <see cref="subModelSeq" /> binding, or if the inferred <c>'id</c> type
    ///   does not match the actual ID type used in that binding.
    /// </summary>
    /// <param name="subModelSeqBindingName">
    ///   The name of the <see cref="subModelSeq" /> binding used as the items source.
    /// </param>
    /// <param name="get">Gets the selected sub-model/sub-binding ID from the model.</param>
    /// <param name="set">
    ///   Returns the message to dispatch on selections/de-selections.
    /// </param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior,
    ///   such as throttling, debouncing, or limiting.
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
