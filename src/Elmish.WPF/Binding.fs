namespace Elmish.WPF


/// Represents all necessary data used to create the different binding types.
type internal BindingData<'model, 'msg> =
  | OneWayData of get: ('model -> obj)
  | OneWayLazyData of
      get: ('model -> obj)
      * map: (obj -> obj)
      * equals: (obj -> obj -> bool)
  | OneWaySeqLazyData of
      get: ('model -> obj)
      * map: (obj -> obj seq)
      * equals: (obj -> obj -> bool)
      * getId: (obj -> obj)
      * itemEquals: (obj -> obj -> bool)
  | TwoWayData of get: ('model -> obj) * set: (obj -> 'model -> 'msg)
  | TwoWayValidateData of
      get: ('model -> obj)
      * set: (obj -> 'model -> 'msg)
      * validate: ('model -> Result<obj, string>)
  | TwoWayIfValidData of
      get: ('model -> obj)
      * set: (obj -> 'model -> Result<'msg, string>)
  | CmdData of
      exec: ('model -> 'msg)
      * canExec: ('model -> bool)
  | CmdIfValidData of exec: ('model -> Result<'msg, obj>)
  | ParamCmdData of
      exec: (obj -> 'model -> 'msg)
      * canExec: (obj -> 'model -> bool)
      * autoRequery: bool
  | SubModelData of
      getModel: ('model -> obj option)
      * getBindings: (unit -> Binding<obj, obj> list)
      * toMsg: (obj -> 'msg)
      * sticky: bool
  | SubModelSeqData of
      getModels: ('model -> obj seq)
      * getId: (obj -> obj)
      * getBindings: (unit -> Binding<obj, obj> list)
      * toMsg: (obj * obj -> 'msg)
  | SubModelSelectedItemData of
      get: ('model -> obj option)
      * set: (obj option -> 'model -> 'msg)
      * subModelSeqBindingName: string


/// Represents all necessary data used to create a binding.
and Binding<'model, 'msg> =
  internal
    { Name: string
      Data: BindingData<'model, 'msg> }



module internal BindingData =

  let box : BindingData<'model, 'msg> -> BindingData<obj, obj> = function
    | OneWayData get -> OneWayData (unbox >> get)
    | OneWayLazyData (get, map, equals) -> OneWayLazyData (unbox >> get, map, equals)
    | OneWaySeqLazyData (get, map, equals, getId, elementEquals) ->
        OneWaySeqLazyData (unbox >> get, map, equals, getId, elementEquals)
    | TwoWayData (get, set) ->
        TwoWayData (unbox >> get, (fun v m -> set v (unbox m) |> box))
    | TwoWayValidateData (get, set, validate) ->
        let boxedSet v m = set v (unbox m) |> box
        TwoWayValidateData (unbox >> get, boxedSet, unbox >> validate)
    | TwoWayIfValidData (get, set) ->
        let boxedSet v m = set v (unbox m) |> Result.map box
        TwoWayIfValidData (unbox >> get, boxedSet)
    | CmdData (exec, canExec) -> CmdData (unbox >> exec >> box, unbox >> canExec)
    | CmdIfValidData exec -> CmdIfValidData (unbox >> exec >> Result.map box)
    | ParamCmdData (exec, canExec, autoRequery) ->
        let boxedExec p m = exec p (unbox m) |> box
        let boxedCanExec p m = canExec p (unbox m)
        ParamCmdData (boxedExec, boxedCanExec, autoRequery)
    | SubModelData (getModel, getBindings, toMsg, sticky) ->
        SubModelData (unbox >> getModel, getBindings, toMsg >> unbox, sticky)
    | SubModelSeqData (getModel, isSame, getBindings, toMsg) ->
        SubModelSeqData (unbox >> getModel, isSame, getBindings, toMsg >> unbox)
    | SubModelSelectedItemData (get, set, subModelSeqBindingName) ->
        SubModelSelectedItemData (unbox >> get, (fun v m -> set v (unbox m) |> box), subModelSeqBindingName)



module Binding =


  /// <summary>Creates a one-way binding.</summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="name">The binding name.</param>
  let oneWay
      (get: 'model -> 'a)
      (name: string)
      : Binding<'model, 'msg> =
    { Name = name
      Data = OneWayData (get >> box) }


  /// <summary>
  ///   Creates a one-way binding to an optional value. The getter automatically
  ///   converts between Option on the source side and a raw (possibly null)
  ///   value on the view side.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="name">The binding name.</param>
  let oneWayOpt
      (get: 'model -> 'a option)
      (name: string)
      : Binding<'model, 'msg> =
    oneWay (get >> Option.map box >> Option.toObj) name


  /// <summary>
  ///   Creates a lazily evaluated one-way binding. The map function will be
  ///   called only when the output of <paramref name="get" /> changes, as
  ///   determined by <paramref name="equals" />. This may have better
  ///   performance than oneWay for expensive computations (but may be less
  ///   performant for non-expensive functions due to additional overhead).
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="equals">
  ///   A function that returns true if the output of the get function and the
  ///   current value are equal.
  ///   For standard equality comparison (e.g. for primitives or structural equality of records),
  ///   you can use the default equality operator (=).
  /// </param>
  /// <param name="map">Transforms the value into the final type.</param>
  /// <param name="name">The binding name.</param>
  let oneWayLazy
      (get: 'model -> 'a)
      (equals: 'a -> 'a -> bool)
      (map: 'a -> 'b)
      (name: string)
      : Binding<'model, 'msg> =
    { Name = name
      Data =
        OneWayLazyData (
          get >> box,
          unbox >> map >> box,
          fun a b -> equals (unbox a) (unbox b))
    }


  /// <summary>
  ///   Creates a one-way binding to a sequence of items, each uniquely identified
  ///   by the value returned by <paramref name="getId"/>. The binding
  ///   is backed by a persistent ObservableCollection, so only changed items
  ///   (as determined by <paramref name="equals"/>) will be replaced. If the
  ///   items are complex and you want them updated instead of replaced, consider
  ///   using subModelSeq.
  /// </summary>
  /// <param name="get">Gets the items from the model.</param>
  /// <param name="getId">Gets a unique identifier for an item.</param>
  /// <param name="itemEquals">
  ///   A function that returns true if two items are equal. For standard equality
  ///   comparison (e.g. for primitives or structural equality of records),
  ///   you can use the default equality operator (=).
  /// </param>
  /// <param name="name">The binding name.</param>
  let oneWaySeq
      (get: 'model -> #seq<'a>)
      (getId: 'a -> 'id)
      (itemEquals: 'a -> 'a -> bool)
      (name: string)
      : Binding<'model, 'msg> =
    let boxedItemEquals (x: obj) (y:obj) = itemEquals (unbox x) (unbox y)
    { Name = name
      Data =
        OneWaySeqLazyData (
          id >> box,
          unbox >> get >> Seq.map box,
          (fun _ _ -> false),
          unbox >> getId >> box,
          boxedItemEquals)
    }


  /// <summary>
  ///   Creates a one-way binding to a sequence of items. The binding will only
  ///   be updated if the output of <paramref name="get" /> changes as determined
  ///   by <paramref name="equals" />. Each item is uniquely identified by the
  ///   value returned by <paramref name="getId"/>. The binding is backed by a
  ///   persistent ObservableCollection, so only changed items (as determined by
  ///   <paramref name="itemEquals"/>) will be replaced. If the items are complex
  ///   and you want them updated instead of replaced, consider using subModelSeq.
  /// </summary>
  /// <param name="get">Gets the items from the model.</param>
  /// <param name="equals">
  ///   A function that returns true if the output of the get function and the
  ///   current value are equal.
  ///   For standard equality comparison (e.g. for primitives or structural equality of records),
  ///   you can use the default equality operator (=).
  /// </param>
  /// <param name="map">Transforms the value into the final type.</param>
  /// <param name="getId">Gets a unique identifier for an item.</param>
  /// <param name="itemEquals">
  ///   A function that returns true if two items are equal. For standard equality
  ///   comparison (e.g. for primitives or structural equality of records),
  ///   you can use the default equality operator (=).
  /// </param>
  /// <param name="name">The binding name.</param>
  let oneWaySeqLazy
      (get: 'model -> 'a)
      (equals: 'a -> 'a -> bool)
      (map: 'a -> #seq<'b>)
      (getId: 'b -> 'id)
      (itemEquals: 'b -> 'b -> bool)
      (name: string)
      : Binding<'model, 'msg> =
    let boxedEquals (x: obj) (y:obj) = equals (unbox x) (unbox y)
    let boxedItemEquals (x: obj) (y:obj) = itemEquals (unbox x) (unbox y)
    { Name = name
      Data =
        OneWaySeqLazyData (
          get >> box,
          unbox >> map >> Seq.map box,
          boxedEquals,
          unbox >> getId >> box,
          boxedItemEquals)
    }


  /// <summary>Creates a two-way binding.</summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="name">The binding name.</param>
  let twoWay
      (get: 'model -> 'a)
      (set: 'a -> 'model -> 'msg)
      (name: string)
      : Binding<'model, 'msg> =
    { Name = name
      Data = TwoWayData (get >> box, unbox >> set) }


  /// <summary>
  ///   Creates a two-way binding to an optional value. The getter/setter
  ///   automatically converts between Option on the source side and a raw
  ///   (possibly null) value on the view side.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="name">The binding name.</param>
  let twoWayOpt
      (get: 'model -> 'a option)
      (set: 'a option -> 'model -> 'msg)
      (name: string)
      : Binding<'model, 'msg> =
    twoWay
      (get >> Option.map box >> Option.toObj)
      (Option.ofObj >> Option.map unbox >> set)
      name


  /// <summary>
  ///   Creates a two-way binding that uses a separate validation function to
  ///   set validation status for the binding target using INotifyDataErrorInfo.
  ///   Messages are dispatched regardless of validation status. Validation is
  ///   carried out whenever the model has been updated.
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="validate">
  ///   Returns the validation message. The contained Ok value will be ignored.
  ///   </param>
  /// <param name="name">The binding name.</param>
  let twoWayValidate
      (get: 'model -> 'a)
      (set: 'a -> 'model -> 'msg)
      (validate: 'model -> Result<'ignored, string>)
      (name: string)
      : Binding<'model, 'msg> =
    { Name = name
      Data = TwoWayValidateData (get >> box, unbox >> set, validate >> Result.map box) }


  /// <summary>
  ///   Creates a two-way binding that uses a validating setter to set validation
  ///   status for the binding target using INotifyDataErrorInfo. Messages are only
  ///   dispatced for valid input (the model will never know about invalid values).
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">
  ///   Returns the message to dispatch or an error message to display.
  /// </param>
  /// <param name="name">The binding name.</param>
  let twoWayIfValid
      (get: 'model -> 'a)
      (set: 'a -> 'model -> Result<'msg, string>)
      (name: string)
      : Binding<'model, 'msg> =
    { Name = name
      Data = TwoWayIfValidData (get >> box, unbox >> set) }


  /// <summary>Creates a command binding that depends only on the model.</summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  /// <param name="name">The binding name.</param>
  let cmd
      (exec: 'model -> 'msg)
      (name: string)
      : Binding<'model, 'msg> =
    { Name = name
      Data = CmdData (exec, fun _ -> true) }


  /// <summary>
  ///   Creates a conditional command binding that depends only on the model.
  ///   CanExecuteChanged will only trigger if the output of canExec changes.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  /// <param name="canExec">Indicates if the command can execute.</param>
  /// <param name="name">The binding name.</param>
  let cmdIf
      (exec: 'model -> 'msg)
      (canExec: 'model -> bool)
      (name: string)
      : Binding<'model, 'msg> =
    { Name = name
      Data = CmdData (exec, fun m -> canExec m) }


  /// <summary>
  ///   Creates a conditional command binding that depends only on the model
  ///   and uses a validation function to determine if it can execute. This allows
  ///   easily re-using the same validation functions for inputs and commands,
  ///   and can also increase type safety by having a single function determine
  ///   both the message and whether it can execute (e.g. by pattern matching against
  ///   a possibly missing value in the model that is needed in the message).
  ///   CanExecuteChanged will only trigger if the validation result changes.
  /// </summary>
  /// <param name="exec">
  ///   Returns the message to dispatch if Ok, or an error that will be ignored
  ///   but will cause the command's CanExecute to return false.
  /// </param>
  /// <param name="name">The binding name.</param>
  let cmdIfValid
      (exec: 'model -> Result<'msg, 'ignored>)
      (name: string)
      : Binding<'model, 'msg> =
    { Name = name
      Data = CmdIfValidData (exec >> Result.mapError box) }


  /// <summary>
  ///   Creates a command binding that depends on the CommandParameter.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  /// <param name="name">The binding name.</param>
  let paramCmd
      (exec: obj -> 'model -> 'msg)
      (name: string)
      : Binding<'model, 'msg> =
    { Name = name
      Data = ParamCmdData (exec, (fun _ _ -> true), false) }


  /// <summary>
  ///   Creates a conditional command binding that depends on the CommandParameter.
  ///   If uiTrigger is false, CanExecuteChanged will only trigger for every model update.
  ///   If uiTrigger is true, CanExecuteChanged will additionally trigger every time
  ///   WPF's CommandManager detects UI changes that could potentially influence
  ///   the command's ability to execute. This will likely lead to many more
  ///   triggers than necessary, but is needed if you have bound the CommandParameter
  ///   to another UI property.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  /// <param name="canExec">Indicates if the command can execute.</param>
  /// <param name="uiTrigger">
  ///   If true, use CommandManager to trigger CanExecuteChanged automatically
  ///   for relevant UI changes.
  /// </param>
  /// <param name="name">The binding name.</param>
  let paramCmdIf
      (exec: obj -> 'model -> 'msg)
      (canExec: obj -> 'model -> bool)
      (uiTrigger: bool)
      (name: string)
      : Binding<'model, 'msg> =
    { Name = name
      Data = ParamCmdData (exec, (fun cmdParam m -> canExec cmdParam m), uiTrigger) }


  /// <summary>
  ///   Creates a binding to a sub-model/component that has its own bindings and possibly
  ///   its own message type. You typically bind this to the DataContext of a UserControl
  ///   or similar.
  /// </summary>
  /// <param name="getSubModel">Gets the sub-model from the model.</param>
  /// <param name="toBindingModel">
  ///   A function to convert the models to the model used by the bindings. You can pass id
  ///   or transform it to your liking.
  /// </param>
  /// <param name="toMsg">
  ///   A function to convert sub-model messages to parent model messages
  ///   (e.g. a parent message union case that wraps the child message type).
  ///   For a sub-model that uses the parent message type, you can pass id.
  /// </param>
  /// <param name="bindings">
  ///   A function that returns the bindings for the sub-model.
  /// </param>
  /// <param name="name">The binding name.</param>
  let subModel
      (getSubModel: 'model -> 'subModel)
      (toBindingModel: 'model * 'subModel -> 'bindingModel)
      (toMsg: 'bindingMsg -> 'msg)
      (bindings: unit -> Binding<'bindingModel, 'bindingMsg> list)
      (name: string)
      : Binding<'model, 'msg> =
    let getBindingModel model =
      toBindingModel (model, (getSubModel model))
    let getBoxedBindings () =
      bindings ()
      |> List.map (fun spec ->
           { Name = spec.Name; Data = BindingData.box spec.Data }
      )
    { Name = name
      Data =
        SubModelData
          ( getBindingModel >> box >> Some,
            getBoxedBindings,
            unbox >> toMsg,
            false) }


  /// <summary>
  ///   Creates a binding to a sub-model/component that has its own bindings and
  ///   possibly its own message type (if not, pass toMsg = id), and may not exist.
  ///   If it does not exist, bindings to this model will return null. You typically
  ///   bind this to the DataContext of a UserControl or similar.
  /// </summary>
  /// <param name="getSubModel">Gets the sub-model from the model.</param>
  /// <param name="toBindingModel">
  ///   A function to convert the models to the model used by the bindings. You can pass id
  ///   or transform it to your liking.
  /// </param>
  /// <param name="toMsg">
  ///   A function to convert sub-model messages to parent model messages
  ///   (e.g. a parent message union case that wraps the child message type).
  ///   For a sub-model that uses the parent message type, you can pass id.
  /// </param>
  /// <param name="bindings">
  ///   A function that returns the bindings for the sub-model.
  /// </param>
  /// <param name="name">The binding name.</param>
  let subModelOpt
      (getSubModel: 'model -> 'subModel option)
      (toBindingModel: 'model * 'subModel -> 'bindingModel)
      (toMsg: 'bindingMsg -> 'msg)
      (bindings: unit -> Binding<'bindingModel, 'bindingMsg> list)
      (name: string)
      : Binding<'model, 'msg> =
    let getBindingModel model =
      getSubModel model |> Option.map (fun sub -> toBindingModel (model, sub))
    let getBoxedBindings () =
      bindings ()
      |> List.map (fun spec ->
           { Name = spec.Name; Data = BindingData.box spec.Data }
      )
    { Name = name
      Data =
        SubModelData
          ( getBindingModel >> Option.map box,
            getBoxedBindings,
            unbox >> toMsg,
            false) }


  /// <summary>
  ///   Creates a binding to a sub-model/component that has its own bindings and
  ///   possibly its own message type (if not, pass toMsg = id), and may not exist.
  ///   If it does not exist, bindings to this model will return the last non-null
  ///   model. You typically bind this to the DataContext of a UserControl or similar.
  ///
  ///   The 'sticky' part is useful if you want to e.g. animate away a UserControl when
  ///   the model is set to None (which will need to be triggered using another binding),
  ///   but don't want the data used by that control to be cleared once the animation starts.
  ///
  ///   Note that since the old view model is used when None is returned, it is technically
  ///   possible that it may continue to send messages even when the model is None.
  ///   For example, if setting the model to None causes a (two-way bound) TextBox to be
  ///   slowly faded away, the user can still type in the text box and messages will still
  ///   be dispatched even though the model is None.
  /// </summary>
  /// <param name="getSubModel">Gets the sub-model from the model.</param>
  /// <param name="toBindingModel">
  ///   A function to convert the models to the model used by the bindings. You can pass id
  ///   or transform it to your liking.
  /// </param>
  /// <param name="toMsg">
  ///   A function to convert sub-model messages to parent model messages
  ///   (e.g. a parent message union case that wraps the child message type).
  ///   For a sub-model that uses the parent message type, you can pass id.
  /// </param>
  /// <param name="bindings">
  ///   A function that returns the bindings for the sub-model.
  /// </param>
  /// <param name="name">The binding name.</param>
  let subModelOptSticky
      (getSubModel: 'model -> 'subModel option)
      (toBindingModel: 'model * 'subModel -> 'bindingModel)
      (toMsg: 'bindingMsg -> 'msg)
      (bindings: unit -> Binding<'bindingModel, 'bindingMsg> list)
      (name: string)
      : Binding<'model, 'msg> =
    let getBindingModel model =
      getSubModel model |> Option.map (fun sub -> toBindingModel (model, sub))
    let getBoxedBindings () =
      bindings ()
      |> List.map (fun spec ->
           { Name = spec.Name; Data = BindingData.box spec.Data }
      )
    { Name = name
      Data =
        SubModelData
          ( getBindingModel >> Option.map box,
            getBoxedBindings,
            unbox >> toMsg,
            true) }


  /// <summary>
  ///   Creates a binding to a sequence of sub-models, each uniquely
  ///   identified by the value returned by the getId function (as determined
  ///   by the default equality comparer). The sub-models have their own bindings
  ///   and possibly their own message type. You typically bind this to the
  ///   ItemsSource of an ItemsControl, ListView, TreeView, etc.
  /// </summary>
  /// <param name="getSubModels">Gets the sub-models from the model.</param>
  /// <param name="toBindingModel">
  ///   A function to convert the models to the model used by the bindings. You can pass id
  ///   or transform it to your liking.
  /// </param>
  /// <param name="getId">Gets a unique identifier for a sub-model.</param>
  /// <param name="toMsg">
  ///   A function to convert a sub-model ID and message to a parent model message.
  ///   (e.g. a parent message union case that wraps the sub-model ID and message type).
  /// </param>
  /// <param name="bindings">
  ///   A function that returns the bindings for the sub-model.
  /// </param>
  /// <param name="name">The binding name.</param>
  let subModelSeq
      (getSubModels: 'model -> #seq<'subModel>)
      (toBindingModel: 'model * 'subModel -> 'bindingModel)
      (getId: 'bindingModel -> 'id)
      (toMsg: ('id * 'bindingMsg) -> 'msg)
      (bindings: unit -> Binding<'bindingModel, 'bindingMsg> list)
      (name: string)
      : Binding<'model, 'msg> =
    let getBindingModels model =
      model |> getSubModels |> Seq.map (fun subModel -> toBindingModel (model, subModel))
    let getBoxedBindings () =
      bindings ()
      |> List.map (fun spec ->
           { Name = spec.Name; Data = BindingData.box spec.Data }
      )
    let boxedToMsg (id: obj, msg: obj) = toMsg (unbox id, unbox msg)
    { Name = name
      Data =
        SubModelSeqData
          ( getBindingModels >> Seq.map box,
            unbox >> getId >> box,
            getBoxedBindings,
            boxedToMsg )
    }


  /// <summary>
  ///   Creates a two-way binding to a SelectedItem-like property where the
  ///   ItemsSource-like property is a subModelSeq. Automatically converts
  ///   the dynamically created Elmish.WPF view models to/from their corresponding
  ///   IDs, so the Elmish user code only has to work with the IDs.
  ///
  ///   Only use this if you are unable to use some kind of SelectedValue or
  ///   SelectedIndex property with a normal twoWay binding. This binding is
  ///   less type-safe and will throw at runtime if itemsSourceBindingName does
  ///   not correspond to a subModelSeq binding, or if the inferred 'id type does
  ///   not match the actual ID type used in that binding.
  /// </summary>
  /// <param name="itemsSourceBindingName">
  ///   The name of the ItemsSource-like binding, which must be created using
  ///   subModelSeq.
  /// </param>
  /// <param name="get">Gets the selected sub-model/sub-binding ID from the model.</param>
  /// <param name="set">
  ///   Returns the message to dispatch on selections/deselections.
  /// </param>
  /// <param name="name">The binding name.</param>
  let subModelSelectedItem
      (itemsSourceBindingName: string)
      (get: 'model -> 'id option)
      (set: 'id option -> 'model -> 'msg)
      (name: string)
      : Binding<'model, 'msg> =
    { Name = name
      Data =
        SubModelSelectedItemData
          ( get >> Option.map box,
            Option.map unbox >> set,
            itemsSourceBindingName )
    }
