namespace Elmish.WPF

open Elmish.WPF.Internal

module Binding =

  /// <summary>Creates a one-way binding.</summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="name">The binding name.</param>
  let oneWay (get: 'model -> 'a) (name: string) =
    { Name = name
      Data = OneWaySpec (get >> box) }

  /// <summary>
  ///   Creates a lazily evaluated one-way binding. The map function will be
  ///   called only when first retrieved and only when the output of the get
  ///   function changes, as determined by the specified equality function.
  ///   This may have better performance than oneWay for expensive computations
  ///   (but may be less performant for non-expensive functions due to additional
  ///   overhead).
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="get">Transforms the value into the final type.</param>
  /// <param name="name">The binding name.</param>
  let oneWayLazyWith (equals: 'a -> 'a -> bool) (get: 'model -> 'a) (map: 'a -> 'b) (name: string) =
    { Name = name
      Data =
        OneWayLazySpec (
          get >> box,
          unbox >> map >> box,
          fun a b -> equals (unbox a) (unbox b))
    }

  /// <summary>
  ///   Alias for oneWayLazyWith (=). Creates a lazily evaluated one-way binding.
  ///   The map function will be called only when first retrieved and only when
  ///   the output of the get function changes. This may have better performance
  ///   than oneWay for expensive computations (but may be less performant for
  ///   non-expensive functions due to additional overhead).
  /// </summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="get">Transforms the value into the final type.</param>
  /// <param name="name">The binding name.</param>
  let oneWayLazy (get: 'model -> 'a) (map: 'a -> 'b) (name: string) =
    oneWayLazyWith (=) get map name

  /// <summary>
  ///   Creates a one-way binding to a sequence of items, each uniquely identified
  ///   by the value returned by the getId function (as determined by the default
  ///   equality comparer). The binding is backed by a persistent ObservableCollection,
  ///   so only changed items will be re-rendered.
  /// </summary>
  /// <param name="get">Gets the items from the model.</param>
  /// <param name="getId">Gets a unique identifier for an item.</param>
  /// <param name="equals">
  ///   A function that returns true if two items are equal. For standard equality
  ///   comparison (e.g. for primitives or structural equality of records),
  ///   you can use the default equality operator (=).
  /// </param>
  /// <param name="name">The binding name.</param>
  let oneWaySeq
      (get: 'model -> #seq<'a>)
      (getId: 'a -> 'id)
      (equals: 'a -> 'a -> bool)
      (name: string) =
    let boxedEquals (x: obj) (y:obj) = equals (unbox x) (unbox y)
    { Name = name
      Data = OneWaySeqSpec ( get >> Seq.map box, unbox >> getId >> box, boxedEquals) }

  /// <summary>Creates a two-way binding.</summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="name">The binding name.</param>
  let twoWay (get: 'model -> 'a) (set: 'a -> 'model -> 'msg) (name: string) =
    { Name = name
      Data = TwoWaySpec (get >> box, unbox >> set) }

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
  //   </param>
  /// <param name="name">The binding name.</param>
  let twoWayValidate
      (get: 'model -> 'a)
      (set: 'a -> 'model -> 'msg)
      (validate: 'model -> Result<'ignored, string>)
      (name: string) =
    { Name = name
      Data = TwoWayValidateSpec (get >> box, unbox >> set, validate >> Result.map box) }

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
      (name: string) =
    { Name = name
      Data = TwoWayIfValidSpec (get >> box, unbox >> set) }

  /// <summary>Creates a command binding that depends only on the model.</summary>
  /// <param name="name">The binding name.</param>
  let cmd (exec: 'model -> 'msg) (name: string) =
    { Name = name
      Data = CmdSpec (exec, fun _ -> true) }

  /// <summary>
  ///   Creates a conditional command binding that depends only on the model.
  ///   CanExecuteChanged will only trigger if the output of canExec changes.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  /// <param name="canExec">Indicates if the command can execute.</param>
  /// <param name="name">The binding name.</param>
  let cmdIf (exec: 'model -> 'msg) (canExec: 'model -> bool) (name: string) =
    { Name = name
      Data = CmdSpec (exec, fun m -> canExec m) }

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
  let cmdIfValid (exec: 'model -> Result<'msg, 'ignored>) (name: string) =
    { Name = name
      Data = CmdIfValidSpec (exec >> Result.mapError box) }

  /// <summary>
  ///   Creates a command binding that depends on the CommandParameter.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  /// <param name="name">The binding name.</param>
  let paramCmd (exec: obj -> 'model -> 'msg) (name: string) =
    { Name = name
      Data = ParamCmdSpec (exec, (fun _ _ -> true), false) }

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
      (name: string) =
    { Name = name
      Data = ParamCmdSpec (exec, (fun cmdParam m -> canExec cmdParam m), uiTrigger) }

  /// <summary>
  ///   Creates a binding to a sub-model/component that has its own bindings.
  ///   You typically bind this to the DataContext of a UserControl or similar.
  /// </summary>
  /// <param name="getModel">Gets the sub-model from the model.</param>
  /// <param name="bindings">
  ///   A function that returns the bindings for the sub-model.
  /// </param>
  /// <param name="toMsg">
  ///   A function to convert sub-model messages to parent model messages
  ///   (e.g. a parent message union case that wraps the child message type).
  /// </param>
  /// <param name="name">The binding name.</param>
  let subModel
      (getModel: 'model -> 'subModel)
      (bindings: unit -> BindingSpec<'subModel, 'subMsg> list)
      (toMsg: 'subMsg -> 'msg)
      (name: string) =
    let getBoxedBindings () =
      bindings ()
      |> List.map (fun spec ->
           { Name = spec.Name; Data = BindingSpecData.box spec.Data }
      )
    { Name = name
      Data = SubModelSpec (getModel >> box >> Some, getBoxedBindings, unbox >> toMsg) }

  /// <summary>
  ///   Creates a binding to a sub-model/component that has its own bindings and
  ///   may not exist. If it does not exist, bindings to this model will return null.
  ///   You typically bind this to the DataContext of a UserControl or similar.
  /// </summary>
  /// <param name="getModel">Gets the sub-model from the model.</param>
  /// <param name="bindings">
  ///   A function that returns the bindings for the sub-model.
  /// </param>
  /// <param name="toMsg">
  ///   A function to convert sub-model messages to parent model messages
  ///   (e.g. a parent message union case that wraps the child message type).
  /// </param>
  /// <param name="name">The binding name.</param>
  let subModelOpt
      (getModel: 'model -> 'subModel option)
      (bindings: unit -> BindingSpec<'subModel, 'subMsg> list)
      (toMsg: 'subMsg -> 'msg)
      (name: string) =
    let getBoxedBindings () =
      bindings ()
      |> List.map (fun spec ->
           { Name = spec.Name; Data = BindingSpecData.box spec.Data }
      )
    { Name = name
      Data = SubModelSpec (getModel >> Option.map box, getBoxedBindings, unbox >> toMsg) }

  /// <summary>
  ///   Creates a binding to a sequence of sub-models, each uniquely identified
  ///   by the value returned by the getId function (as determined by the default
  ///   equality comparer). You typically bind this to the ItemsSource of an
  ///   ItemsControl, ListView, TreeView, etc.
  /// </summary>
  /// <param name="getModels">Gets the sub-models from the model.</param>
  /// <param name="getId">Gets a unique identifier for a sub-model.</param>
  /// <param name="bindings">
  ///   A function that returns the bindings for the sub-model.
  /// </param>
  /// <param name="toMsg">
  ///   A function to convert sub-model messages to parent model messages
  ///   (e.g. a parent message union case that wraps the child message type).
  /// </param>
  /// <param name="name">The binding name.</param>
  let subModelSeq
      (getModels: 'model -> #seq<'subModel>)
      (getId: 'subModel -> 'id)
      (bindings: unit -> BindingSpec<'subModel, 'subMsg> list)
      (toMsg: ('id * 'subMsg) -> 'msg)
      (name: string) =
    let getBoxedBindings () =
      bindings ()
      |> List.map (fun spec ->
           { Name = spec.Name; Data = BindingSpecData.box spec.Data }
      )
    let boxedToMsg (id: obj, msg: obj) = toMsg (unbox id, unbox msg)
    { Name = name
      Data =
        SubModelSeqSpec
          ( getModels >> Seq.map box,
            unbox >> getId >> box,
            getBoxedBindings,
            boxedToMsg )
    }
