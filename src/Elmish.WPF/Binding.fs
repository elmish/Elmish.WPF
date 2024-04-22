namespace Elmish.WPF

open System.Windows

open Elmish
open System.Windows.Input
open System.Collections.ObjectModel


module Binding =
  open BindingData

  let internal mapData f binding =
    { Name = binding.Name
      Data = binding.Data |> f }

  /// Boxes the output parameter.
  /// Allows using a strongly-typed submodel binding (from a module ending in "T")
  /// in a binding list (rather than in a view model class member as normal).
  let boxT (binding: Binding<'b, 'msg, 't>) = BindingData.boxT |> mapData <| binding

  /// Unboxes the output parameter
  let unboxT (binding: Binding<'b, 'msg>): Binding<'b, 'msg, 't> = BindingData.unboxT |> mapData <| binding

  /// Maps the model of a binding via a contravariant mapping.
  let mapModel (f: 'a -> 'b) (binding: Binding<'b, 'msg, 't>) = f |> mapModel |> mapData <| binding

  /// Maps the message of a binding with access to the model via a covariant mapping.
  let mapMsgWithModel (f: 'a -> 'model -> 'b) (binding: Binding<'model, 'a, 't>) = f |> mapMsgWithModel |> mapData <| binding

  /// Maps the message of a binding via a covariant mapping.
  let mapMsg (f: 'a -> 'b) (binding: Binding<'model, 'a, 't>) = f |> mapMsg |> mapData <| binding

  /// Sets the message of a binding with access to the model.
  let setMsgWithModel (f: 'model -> 'b) (binding: Binding<'model, 'a, 't>) = f |> setMsgWithModel |> mapData <| binding

  /// Sets the message of a binding.
  let setMsg (msg: 'b) (binding: Binding<'model, 'a, 't>) = msg |> setMsg |> mapData <| binding


  /// Restricts the binding to models that satisfy the predicate after some model satisfies the predicate.
  let addSticky (predicate: 'model -> bool) (binding: Binding<'model, 'msg, 't>) = predicate |> addSticky |> mapData <| binding

  /// <summary>
  ///   Adds caching to the given binding.  The cache holds a single value and
  ///   is invalidated after the given binding raises the
  ///   <c>PropertyChanged</c> event.
  /// </summary>
  /// <param name="binding">The binding to which caching is added.</param>
  let addCaching (binding: Binding<'model, 'msg, 't>) : Binding<'model, 'msg, 't> =
    binding
    |> mapData addCaching

  /// <summary>
  ///   Adds validation to the given binding using <c>INotifyDataErrorInfo</c>.
  /// </summary>
  /// <param name="validate">Returns the errors associated with the given model.</param>
  /// <param name="binding">The binding to which validation is added.</param>
  let addValidation (validate: 'model -> string list) (binding: Binding<'model, 'msg, 't>) : Binding<'model, 'msg, 't> =
    binding
    |> mapData (addValidation validate)

  /// <summary>
  ///   Adds laziness to the updating of the given binding. If the models are considered equal,
  ///   then updating of the given binding is skipped.
  /// </summary>
  /// <param name="equals">Updating skipped when this function returns <c>true</c>.</param>
  /// <param name="binding">The binding to which the laziness is added.</param>
  let addLazy (equals: 'model -> 'model -> bool) (binding: Binding<'model, 'msg, 't>) : Binding<'model, 'msg, 't> =
    binding
    |> mapData (addLazy equals)

  /// <summary>
  ///   Alters the message stream via the given function.
  ///   Ideally suited for use with Reactive Extensions.
  ///   <code>
  ///     open FSharp.Control.Reactive
  ///     let delay dispatch =
  ///       let subject = Subject.broadcast
  ///       let observable = subject :&gt; System.IObservable&lt;_&gt;
  ///       observable
  ///       |&gt; Observable.delay (System.TimeSpan.FromSeconds 1.0)
  ///       |&gt; Observable.subscribe dispatch
  ///       |&gt; ignore
  ///       subject.OnNext
  ///
  ///     // ...
  ///
  ///     binding |&gt; Binding.alterMsgStream delay
  ///   </code>
  /// </summary>
  /// <param name="alteration">The function that can alter the message stream.</param>
  /// <param name="binding">The binding of the altered message stream.</param>
  let alterMsgStream (alteration: ('b -> unit) -> 'a -> unit) (binding: Binding<'model, 'a, 't>) : Binding<'model, 'b, 't> =
    binding
    |> mapData (alterMsgStream alteration)


  /// <summary>
  ///   Strongly-typed bindings that update the view from the model.
  /// </summary>
  module OneWayT =

    /// Elemental instance of a one-way binding.
    let id<'a, 'msg> : string -> Binding<'a, 'msg, 'a> =
      OneWay.id
      |> createBindingT

  /// <summary>
  ///   Strongly-typed bindings that update the model from the view.
  /// </summary>
  module OneWayToSourceT =

    /// Elemental instance of a one-way-to-source binding.
    let id<'model, 'a> : string -> Binding<'model, 'a, 'a> =
      OneWayToSource.id
      |> createBindingT

  /// <summary>
  ///   Strongly-typed bindings that update both ways
  /// </summary>
  module TwoWayT =

    /// Elemental instance of a two-way binding.
    let id<'a> : string -> Binding<'a, 'a, 'a> =
      TwoWay.id
      |> createBindingT

  /// <summary>
  ///   Strongly-typed bindings that dispatch messages from the view.
  /// </summary>
  module CmdT =

    /// <summary>
    ///   Elemental instance of a <c>Command</c> binding.
    ///   Creates a <c>Command</c> binding that only passes the <c>CommandParameter</c>)
    /// </summary>
    /// <param name="uiBoundCmdParam">
    ///   If <c>true</c>, <c>CanExecuteChanged</c> will trigger every time WPF's
    ///   <c>CommandManager</c>
    ///   detects UI changes that could potentially influence the command's
    ///   ability to execute. This will likely lead to many more triggers than
    ///   necessary, but is needed if you have bound the <c>CommandParameter</c>
    ///   to another UI property.
    /// </param>
    /// <param name="canExec">Indicates whether the command can execute.</param>
    let id<'model> uiBoundCmdParam canExec
        : string -> Binding<'model, obj, ICommand> =
      Cmd.createWithParam
        (fun p _ -> ValueSome p)
        canExec
        uiBoundCmdParam
      |> createBindingT

    /// <summary>
    ///   Creates a <c>Command</c> binding that depends only on the model (not the
    ///   <c>CommandParameter</c>).
    /// </summary>
    /// <param name="canExec">Indicates whether the command can execute.</param>
    /// <param name="exec">Returns the message to dispatch.</param>
    let model
        canExec
        (exec: 'model -> 'msg)
        : string -> Binding<'model, 'msg, ICommand> =
      id false (fun _ m -> m |> canExec)
      >> mapMsgWithModel (fun _ y -> y |> exec)
      >> addLazy (fun m1 m2 -> canExec m1 = canExec m2)

    /// <summary>
    ///   Creates a <c>Command</c> binding that dispatches the specified message.
    /// </summary>
    /// <param name="canExec">Indicates whether the command can execute.</param>
    /// <param name="msg">The message to dispatch.</param>
    let set
        canExec
        (msg: 'msg)
        : string -> Binding<'model, 'msg, ICommand> =
      id false (fun _ m -> m |> canExec)
      >> setMsg msg

    /// <summary>
    ///   Creates a <c>Command</c> binding that depends only on the model (not the
    ///   <c>CommandParameter</c>) and always executes.
    /// </summary>
    /// <param name="exec">Returns the message to dispatch.</param>
    let modelAlways
        (exec: 'model -> 'msg)
        : string -> Binding<'model, 'msg, ICommand> =
      model (fun _ -> true) exec

    /// <summary>
    ///   Creates a <c>Command</c> binding that dispatches the specified message
    ///   and always executes.
    /// </summary>
    /// <param name="msg">The message to dispatch.</param>
    let setAlways
        (msg: 'msg)
        : string -> Binding<'model, 'msg, ICommand> =
      set (fun _ -> true) msg

  module OneWay =

    /// Elemental instance of a one-way binding.
    let id<'a, 'msg> : string -> Binding<'a, 'msg> =
      OneWay.id
      |> createBinding

    /// Creates a one-way binding to an optional value. The binding
    /// automatically converts between a missing value in the model and
    /// a <c>null</c> value in the view.
    let opt<'a, 'msg> : string -> Binding<'a option, 'msg> =
      id<obj, 'msg>
      >> mapModel Option.box

    /// Creates a one-way binding to an optional value. The binding
    /// automatically converts between a missing value in the model and
    /// a <c>null</c> value in the view.
    let vopt<'a, 'msg> : string -> Binding<'a voption, 'msg> =
      id<obj, 'msg>
      >> mapModel ValueOption.box


  module OneWayToSource =

    /// Elemental instance of a one-way-to-source binding.
    let id<'model, 'a> : string -> Binding<'model, 'a> =
      OneWayToSource.id
      |> createBinding

    /// Creates a one-way-to-source binding to an optional value. The binding
    /// automatically converts between a missing value in the model and
    /// a <c>null</c> value in the view.
    let vopt<'model, 'a> : string -> Binding<'model, 'a voption> =
      id<'model, obj>
      >> mapMsg ValueOption.unbox

    /// Creates a one-way-to-source binding to an optional value. The binding
    /// automatically converts between a missing value in the model and
    /// a <c>null</c> value in the view.
    let opt<'model, 'a> : string -> Binding<'model, 'a option> =
      id<'model, obj>
      >> mapMsg Option.unbox


  module TwoWay =

    /// Elemental instance of a two-way binding.
    let id<'a> : string -> Binding<'a, 'a> =
      TwoWay.id
      |> createBinding

    /// Creates a one-way-to-source binding to an optional value. The binding
    /// automatically converts between a missing value in the model and
    /// a <c>null</c> value in the view.
    let vopt<'a> : string -> Binding<'a voption, 'a voption> =
      id<obj>
      >> mapModel ValueOption.box
      >> mapMsg ValueOption.unbox

    /// Creates a one-way-to-source binding to an optional value. The binding
    /// automatically converts between a missing value in the model and
    /// a <c>null</c> value in the view.
    let opt<'a> : string -> Binding<'a option, 'a option> =
      id<obj>
      >> mapModel Option.box
      >> mapMsg Option.unbox


  module SubModelSelectedItem =

    /// Creates a two-way binding to a <c>SelectedItem</c>-like property where
    /// the <c>ItemsSource</c>-like property is a <see cref="subModelSeq" />
    /// binding. Automatically converts the dynamically created Elmish.WPF view
    /// models to/from their corresponding IDs, so the Elmish user code only has
    /// to work with the IDs.
    ///
    /// Only use this if you are unable to use some kind of <c>SelectedValue</c>
    /// or <c>SelectedIndex</c> property with a normal <see cref="twoWay" />
    /// binding. This binding is less type-safe. It will throw when initializing
    /// the bindings if <paramref name="subModelSeqBindingName" />
    /// does not correspond to a <see cref="subModelSeq" /> binding, and it will
    /// throw at runtime if the inferred <c>'id</c> type does not match the
    /// actual ID type used in that binding.
    let vopt subModelSeqBindingName : string -> Binding<'id voption, 'id voption> =
      SubModelSelectedItem.create subModelSeqBindingName
      |> createBinding
      >> mapModel (ValueOption.map box)
      >> mapMsg (ValueOption.map unbox)

    /// Creates a two-way binding to a <c>SelectedItem</c>-like property where
    /// the <c>ItemsSource</c>-like property is a <see cref="subModelSeq" />
    /// binding. Automatically converts the dynamically created Elmish.WPF view
    /// models to/from their corresponding IDs, so the Elmish user code only has
    /// to work with the IDs.
    ///
    /// Only use this if you are unable to use some kind of <c>SelectedValue</c>
    /// or <c>SelectedIndex</c> property with a normal <see cref="twoWay" />
    /// binding. This binding is less type-safe. It will throw when initializing
    /// the bindings if <paramref name="subModelSeqBindingName" />
    /// does not correspond to a <see cref="subModelSeq" /> binding, and it will
    /// throw at runtime if the inferred <c>'id</c> type does not match the
    /// actual ID type used in that binding.
    let opt subModelSeqBindingName : string -> Binding<'id option, 'id option> =
      vopt subModelSeqBindingName
      >> mapModel ValueOption.ofOption
      >> mapMsg ValueOption.toOption


  module Cmd =

    let internal createWithParam exec canExec autoRequery =
      Cmd.createWithParam exec canExec autoRequery
      |> createBinding

    let internal create exec canExec =
      createWithParam
        (fun _ -> exec)
        (fun _ -> canExec)
        false
      >> addLazy (fun m1 m2 -> canExec m1 = canExec m2)


  module OneWaySeq =

    let internal create get itemEquals getId =
      OneWaySeq.create itemEquals getId
      |> BindingData.mapModel get
      |> createBinding


  module SubModel =

    /// <summary>
    ///   Creates a binding to a sub-model/component. You typically bind this
    ///   to the <c>DataContext</c> of a <c>UserControl</c> or similar.
    /// </summary>
    /// <param name="bindings">Returns the bindings for the sub-model.</param>
    let vopt (bindings: unit -> Binding<'model, 'msg> list)
        : string -> Binding<'model voption, 'msg> =
      SubModel.create
        (fun args -> DynamicViewModel<'model, 'msg>(args, bindings ()))
        IViewModel.updateModel
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

  /// <summary>
  ///   The strongly-typed counterpart of module <c>SubModel</c>.
  ///   For creating bindings to child view models that have their own bindings.
  ///   Typically bound from WPF using <c>DataContext</c> and <c>Binding</c>.
  ///   Can be used in binding lists if boxed using <see cref="boxT" />.
  /// </summary>
  module SubModelT =

    /// Exposes an optional view model member for binding.
    let opt
      (createVm: ViewModelArgs<'bindingModel, 'msg> -> #IViewModel<'bindingModel, 'msg>)
      : (string -> Binding<'bindingModel voption, 'msg, #IViewModel<'bindingModel, 'msg>>)
      =
      SubModel.create createVm IViewModel.updateModel
      |> createBindingT

    /// Exposes a non-optional view model member for binding.
    let req
      (createVm: ViewModelArgs<'bindingModel, 'msg> -> #IViewModel<'bindingModel, 'msg>)
      : (string -> Binding<'bindingModel, 'msg, #IViewModel<'bindingModel, 'msg>>)
      =
      SubModel.create createVm IViewModel.updateModel
      |> createBindingT
      >> mapModel ValueSome

    /// <summary>
    ///   Exposes a <c>'a seq</c> (<c>IEnumerable&lt;'a&gt;</c>) view model member for binding.
    ///   Used rarely; usually, you want to expose an <c>ObservableCollection&lt;'a&gt;</c>
    ///   using <c>SubModelSeqKeyedT</c> or <c>SubModelSeqUnkeyedT</c>.
    /// </summary>
    let seq
      (createVm: ViewModelArgs<'bindingModel, 'msg> -> #seq<#IViewModel<'bindingModel, 'msg>>)
      : (string -> Binding<'bindingModel, 'msg, #seq<#IViewModel<'bindingModel, 'msg>>>)
      =
      SubModel.create createVm (fun (vms, m) -> vms |> Seq.iter (fun vm -> IViewModel.updateModel (vm, m)))
      |> createBindingT
      >> mapModel ValueSome

  /// <summary>
  ///   The strongly-typed counterpart of <c>Binding.subModelSeq</c> without parameter <c>getId</c>.
  ///   Exposes an <c>ObservableCollection</c> of child view models for binding.
  ///   Identifies elements by index;
  ///   if possible, use <c>SubModelSeqKeyedT</c> (which uses parameter <c>getId</c>) instead.
  ///   Typically bound from WPF using <c>DataContext</c> and <c>Binding</c>.
  ///   Can be used in binding lists if boxed using <see cref="boxT" />.
  /// </summary>
  module SubModelSeqUnkeyedT =

    /// <summary>
    ///   Creates an elemental <c>SubModelSeqUnkeyedT</c> binding.
    /// </summary>
    /// <param name="createVm">
    ///   The function applied to every element of the bound <c>ObservableCollection</c>
    ///   to create a child view model.
    /// </param>
    let id
      (createVm: ViewModelArgs<'bindingModel, 'msg> -> #IViewModel<'bindingModel, 'msg>)
      : (string -> Binding<'bindingModelCollection, int * 'msg, ObservableCollection<#IViewModel<'bindingModel, 'msg>>>)
      =
      SubModelSeqUnkeyed.create createVm IViewModel.updateModel
      |> createBindingT

  /// <summary>
  ///   The strongly-typed counterpart of <c>Binding.subModelSeq</c> with parameter <c>getId</c>.
  ///   Exposes an <c>ObservableCollection</c> of child view models for binding.
  ///   Typically bound from WPF using <c>DataContext</c> and <c>Binding</c>.
  ///   Can be used in binding lists if boxed using <see cref="boxT" />.
  /// </summary>
  module SubModelSeqKeyedT =

    /// <summary>
    ///   Creates an elemental <c>SubModelSeqUnkeyedT</c> binding.
    /// </summary>
    /// <param name="createVm">
    ///   The function applied to every element of the bound <c>ObservableCollection</c>
    ///   to create a child view model.
    /// </param>
    /// <param name="getId">
    ///   The function applied to every element of the bound <c>ObservableCollection</c>
    ///   to get a key used to identify that element.
    ///   Should not return duplicate keys for different elements.
    /// </param>
    let id
      (createVm: ViewModelArgs<'bindingModel, 'msg> -> #IViewModel<'bindingModel, 'msg>)
      (getId: 'bindingModel -> 'id)
      : (string -> Binding<'bindingModelCollection, 'id * 'msg, ObservableCollection<#IViewModel<'bindingModel, 'msg>>>)
      =
      SubModelSeqKeyed.create createVm IViewModel.updateModel getId (IViewModel.currentModel >> getId)
      |> createBindingT

  /// <summary>
  ///   The strongly-typed counterpart of <c>Binding.subModelWin</c>.
  ///   Like <see cref="SubModelT.opt" />, but uses the <c>WindowState</c> wrapper
  ///   to show/hide/close a new window that will have the specified bindings as
  ///   its <c>DataContext</c>.
  ///
  ///   You do not need to set the <c>DataContext</c> yourself (either in code
  ///   or in XAML).
  ///
  ///   Can be used in binding lists if boxed using <see cref="boxT" />.
  /// </summary>
  module SubModelWinT =

    /// <summary>
    ///   Creates an elemental <c>SubModelWinT</c> binding.
    ///   Like <see cref="SubModelT.opt" />, but uses the <c>WindowState</c> wrapper
    ///   to show/hide/close a new window that will have the specified bindings as
    ///   its <c>DataContext</c>.
    ///
    ///   You do not need to set the <c>DataContext</c> yourself (either in code
    ///   or in XAML).
    ///   The window can only be closed/hidden by changing the return value of
    ///   <paramref name="getState" />, and cannot be directly closed by the
    ///   user. External close attempts (the Close/X button, Alt+F4, or System
    ///   Menu -> Close) will cause the message specified by
    ///   <paramref name="onCloseRequested" /> to be dispatched. You should supply
    ///   <paramref name="onCloseRequested" /> and react to this in a manner that
    ///   will not confuse a user trying to close the window (e.g. by closing it
    ///   or displaying relevant feedback to the user).
    /// </summary>
    /// <param name="getState">Gets the window state and a sub-model.</param>
    /// <param name="createVM">Returns the view model for the window.</param>
    /// <param name="getWindow">
    ///   The function used to get and configure the window.
    /// </param>
    /// <param name="isModal">
    ///   Specifies whether the window will be shown modally (using
    ///   <c>Window.ShowDialog</c>, blocking the rest of the app) or non-modally (using
    ///   <c>Window.Show</c>).
    /// </param>
    /// <param name="onCloseRequested">
    ///   The message to be dispatched on external close attempts (the Close/X
    ///   button, Alt+F4, or System Menu -> Close).
    /// </param>
    let id
      (getState: 'model -> WindowState<'bindingModel>)
      (createVM: ViewModelArgs<'bindingModel, 'bindingMsg> -> #IViewModel<'bindingModel, 'bindingMsg>)
      getWindow isModal onCloseRequested =
      SubModelWin.create getState createVM IViewModel.updateModel Func2.id2 getWindow isModal onCloseRequested
      |> createBindingT


  module SelectedIndex =
    /// Prebuilt binding intended for use with <code>Selector.SelectedIndex</code>.
    let vopt =
      TwoWay.id
      >> mapModel (ValueOption.defaultValue -1)
      >> mapMsg (fun i -> if i < 0 then ValueNone else ValueSome i)

    /// Prebuilt binding intended for use with <code>Selector.SelectedIndex</code>.
    let opt =
      vopt
      >> mapModel ValueOption.ofOption
      >> mapMsg ValueOption.toOption


  module SubModelWin =

    let internal create getState createViewModel updateViewModel toMsg getWindow isModal onCloseRequested =
      SubModelWin.create getState createViewModel updateViewModel toMsg getWindow isModal onCloseRequested
      |> createBinding


  module SubModelSeqUnkeyed =

    let internal create createViewModel updateViewModel =
      SubModelSeqUnkeyed.create createViewModel updateViewModel
      |> createBinding


  module SubModelSeqKeyed =

    let internal create createViewModel updateViewModel bmToId vmToId =
      SubModelSeqKeyed.create createViewModel updateViewModel bmToId vmToId
      |> createBinding


module Bindings =

  /// Maps the model of a list of bindings via a contravariant mapping.
  let mapModel (f: 'a -> 'b) (bindings: Binding<'b, 'msg> list) = f |> Binding.mapModel |> List.map <| bindings

  /// Maps the message of a list of bindings with access to the model via a covariant mapping.
  let mapMsgWithModel (f: 'a -> 'model -> 'b) (bindings: Binding<'model, 'a> list) = f |> Binding.mapMsgWithModel |> List.map <| bindings

  /// Maps the message of a list of bindings via a covariant mapping.
  let mapMsg (f: 'a -> 'b) (bindings: Binding<'model, 'a> list) = f |> Binding.mapMsg |> List.map <| bindings


[<AbstractClass; Sealed>]
type Binding private () =

  /// <summary>
  ///   Creates a binding intended for use with <code>Selector.SelectedIndex</code>.
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
  ///   Creates a binding intended for use with <code>Selector.SelectedIndex</code>.
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
    >> Binding.mapMsgWithModel set

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
    >> Binding.mapMsgWithModel set

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
    >> Binding.mapMsgWithModel set


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
    Binding.OneWaySeq.create map itemEquals getId
    >> Binding.addLazy equals
    >> Binding.mapModel get


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
    Binding.OneWaySeq.create id itemEquals getId
    >> Binding.addLazy refEq
    >> Binding.mapModel get


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
    >> Binding.mapMsgWithModel set

  /// <summary>Creates a two-way binding.</summary>
  /// <param name="get">Gets the value from the model.</param>
  /// <param name="set">Returns the message to dispatch.</param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
  static member twoWay
      (get: 'model -> 'a,
       set: 'a -> 'model -> 'msg,
       wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    Binding.twoWay (get, set)
    >> Binding.alterMsgStream wrapDispatch


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
    >> Binding.mapMsgWithModel set

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
  [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
  static member twoWayOpt
      (get: 'model -> 'a option,
       set: 'a option -> 'model -> 'msg,
       wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    Binding.twoWayOpt (get, set)
    >> Binding.alterMsgStream wrapDispatch


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
    >> Binding.mapMsgWithModel set

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
  [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
  static member twoWayOpt
      (get: 'model -> 'a voption,
       set: 'a voption -> 'model -> 'msg,
       wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    Binding.twoWayOpt (get, set)
    >> Binding.alterMsgStream wrapDispatch


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
    >> Binding.mapMsgWithModel set
    >> Binding.addValidation validate

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
  [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
  static member twoWayValidate
      (get: 'model -> 'a,
       set: 'a -> 'model -> 'msg,
       validate: 'model -> string list,
       wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    Binding.twoWayValidate (get, set, validate)
    >> Binding.alterMsgStream wrapDispatch


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
    >> Binding.mapMsgWithModel set
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
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
  static member twoWayValidate
      (get: 'model -> 'a,
       set: 'a -> 'model -> 'msg,
       validate: 'model -> string voption,
       wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    Binding.twoWayValidate (get, set, validate)
    >> Binding.alterMsgStream wrapDispatch


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
    >> Binding.mapMsgWithModel set
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
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
  static member twoWayValidate
      (get: 'model -> 'a,
       set: 'a -> 'model -> 'msg,
       validate: 'model -> string option,
       wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    Binding.twoWayValidate (get, set, validate)
    >> Binding.alterMsgStream wrapDispatch


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
    >> Binding.mapMsgWithModel set
    >> Binding.addValidation (validate >> ValueOption.ofError >> ValueOption.toList)

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
  [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
  static member twoWayValidate
      (get: 'model -> 'a,
       set: 'a -> 'model -> 'msg,
       validate: 'model -> Result<'ignored, string>,
       wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    Binding.twoWayValidate (get, set, validate)
    >> Binding.alterMsgStream wrapDispatch


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
    >> Binding.mapMsgWithModel set
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
  ///   Returns the validation messages from the updated model.
  /// </param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
  static member twoWayOptValidate
      (get: 'model -> 'a voption,
       set: 'a voption -> 'model -> 'msg,
       validate: 'model -> string list,
       wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    Binding.twoWayOptValidate (get, set, validate)
    >> Binding.alterMsgStream wrapDispatch


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
    >> Binding.mapMsgWithModel set
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
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
  static member twoWayOptValidate
      (get: 'model -> 'a voption,
       set: 'a voption -> 'model -> 'msg,
       validate: 'model -> string voption,
       wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    Binding.twoWayOptValidate (get, set, validate)
    >> Binding.alterMsgStream wrapDispatch


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
    >> Binding.mapMsgWithModel set
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
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
  static member twoWayOptValidate
      (get: 'model -> 'a voption,
       set: 'a voption -> 'model -> 'msg,
       validate: 'model -> string option,
       wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    Binding.twoWayOptValidate (get, set, validate)
    >> Binding.alterMsgStream wrapDispatch


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
    >> Binding.mapMsgWithModel set
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
  ///   Returns the validation message from the updated model.
  /// </param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
  static member twoWayOptValidate
      (get: 'model -> 'a voption,
       set: 'a voption -> 'model -> 'msg,
       validate: 'model -> Result<'ignored, string>,
       wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    Binding.twoWayOptValidate (get, set, validate)
    >> Binding.alterMsgStream wrapDispatch


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
    >> Binding.mapMsgWithModel set
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
  ///   Returns the validation messages from the updated model.
  /// </param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
  static member twoWayOptValidate
      (get: 'model -> 'a option,
       set: 'a option -> 'model -> 'msg,
       validate: 'model -> string list,
       wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    Binding.twoWayOptValidate (get, set, validate)
    >> Binding.alterMsgStream wrapDispatch


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
    >> Binding.mapMsgWithModel set
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
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
  static member twoWayOptValidate
      (get: 'model -> 'a option,
       set: 'a option -> 'model -> 'msg,
       validate: 'model -> string voption,
       wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    Binding.twoWayOptValidate (get, set, validate)
    >> Binding.alterMsgStream wrapDispatch


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
    >> Binding.mapMsgWithModel set
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
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
  static member twoWayOptValidate
      (get: 'model -> 'a option,
       set: 'a option -> 'model -> 'msg,
       validate: 'model -> string option,
       wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    Binding.twoWayOptValidate (get, set, validate)
    >> Binding.alterMsgStream wrapDispatch


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
    >> Binding.mapMsgWithModel set
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
  ///   Returns the validation message from the updated model.
  /// </param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
  static member twoWayOptValidate
      (get: 'model -> 'a option,
       set: 'a option -> 'model -> 'msg,
       validate: 'model -> Result<'ignored, string>,
       wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    Binding.twoWayOptValidate (get, set, validate)
    >> Binding.alterMsgStream wrapDispatch


  /// <summary>
  ///   Creates a <c>Command</c> binding that depends only on the model (not the
  ///   <c>CommandParameter</c>) and can always execute.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  static member cmd
      (exec: 'model -> 'msg)
      : string -> Binding<'model, 'msg> =
    Binding.Cmd.create
      (exec >> ValueSome)
      (fun _ -> true)

  /// <summary>
  ///   Creates a <c>Command</c> binding that depends only on the model (not the
  ///   <c>CommandParameter</c>) and can always execute.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
  static member cmd
      (exec: 'model -> 'msg,
       wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    Binding.cmd exec
    >> Binding.alterMsgStream wrapDispatch


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
    Binding.Cmd.create
      (exec >> ValueSome)
      canExec

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
  [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
  static member cmdIf
      (exec: 'model -> 'msg,
       canExec: 'model -> bool,
       wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    Binding.cmdIf (exec, canExec)
    >> Binding.alterMsgStream wrapDispatch


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
    Binding.Cmd.create
      exec
      (exec >> ValueOption.isSome)

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
  [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
  static member cmdIf
      (exec: 'model -> 'msg voption,
       wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    Binding.cmdIf exec
    >> Binding.alterMsgStream wrapDispatch


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
    Binding.Cmd.create
      (exec >> ValueOption.ofOption)
      (exec >> Option.isSome)

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
  [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
  static member cmdIf
      (exec: 'model -> 'msg option,
       wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    Binding.cmdIf exec
    >> Binding.alterMsgStream wrapDispatch


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
    Binding.Cmd.create
      (exec >> ValueOption.ofOk)
      (exec >> Result.isOk)

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
  [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
  static member cmdIf
      (exec: 'model -> Result<'msg, 'ignored>,
       wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    Binding.cmdIf exec
    >> Binding.alterMsgStream wrapDispatch


  /// <summary>
  ///   Creates a <c>Command</c> binding that depends on the
  ///   <c>CommandParameter</c>
  ///   and can always execute.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  static member cmdParam
      (exec: obj -> 'model -> 'msg)
      : string -> Binding<'model, 'msg> =
    Binding.Cmd.createWithParam
      (fun p model -> exec p model |> ValueSome)
      (fun _ _ -> true)
      false

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
  [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
  static member cmdParam
      (exec: obj -> 'model -> 'msg,
       wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    Binding.cmdParam exec
    >> Binding.alterMsgStream wrapDispatch


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
    Binding.Cmd.createWithParam
      (fun p m -> exec p m |> ValueSome)
      canExec
      (defaultArg uiBoundCmdParam false)

  /// <summary>
  ///   Creates a <c>Command</c> binding that depends on the
  ///   <c>CommandParameter</c>
  ///   and can execute if <paramref name="canExec" /> returns <c>true</c>.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  /// <param name="canExec">Indicates whether the command can execute.</param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
  static member cmdParamIf
      (exec: obj -> 'model -> 'msg,
       canExec: obj -> 'model -> bool,
       wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    Binding.cmdParamIf (exec, canExec)
    >> Binding.alterMsgStream wrapDispatch

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
  [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
  static member cmdParamIf
      (exec: obj -> 'model -> 'msg,
       canExec: obj -> 'model -> bool,
       uiBoundCmdParam: bool,
       wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    Binding.cmdParamIf (exec, canExec, uiBoundCmdParam)
    >> Binding.alterMsgStream wrapDispatch


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
    Binding.Cmd.createWithParam
      exec
      (fun p m -> exec p m |> ValueOption.isSome)
      (defaultArg uiBoundCmdParam false)

  /// <summary>
  ///   Creates a conditional <c>Command</c> binding that depends on the
  ///   <c>CommandParameter</c>
  ///   and can execute if <paramref name="exec" /> returns <c>ValueSome</c>.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
  static member cmdParamIf
      (exec: obj -> 'model -> 'msg voption,
       wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    Binding.cmdParamIf exec
    >> Binding.alterMsgStream wrapDispatch

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
  [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
  static member cmdParamIf
      (exec: obj -> 'model -> 'msg voption,
       uiBoundCmdParam: bool,
       wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    Binding.cmdParamIf (exec, uiBoundCmdParam)
    >> Binding.alterMsgStream wrapDispatch


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
    Binding.Cmd.createWithParam
      (fun p m -> exec p m |> ValueOption.ofOption)
      (fun p m -> exec p m |> Option.isSome)
      (defaultArg uiBoundCmdParam false)

  /// <summary>
  ///   Creates a conditional <c>Command</c> binding that depends on the
  ///   <c>CommandParameter</c>
  ///   and can execute if <paramref name="exec" /> returns <c>Some</c>.
  /// </summary>
  /// <param name="exec">Returns the message to dispatch.</param>
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
  static member cmdParamIf
      (exec: obj -> 'model -> 'msg option,
       wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    Binding.cmdParamIf exec
    >> Binding.alterMsgStream wrapDispatch

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
  [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
  static member cmdParamIf
      (exec: obj -> 'model -> 'msg option,
       uiBoundCmdParam: bool,
       wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    Binding.cmdParamIf (exec, uiBoundCmdParam)
    >> Binding.alterMsgStream wrapDispatch


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
    Binding.Cmd.createWithParam
      (fun p m -> exec p m |> ValueOption.ofOk)
      (fun p m -> exec p m |> Result.isOk)
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
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
  static member cmdParamIf
      (exec: obj -> 'model -> Result<'msg, 'ignored>,
       wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    Binding.cmdParamIf exec
    >> Binding.alterMsgStream wrapDispatch

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
  [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
  static member cmdParamIf
      (exec: obj -> 'model -> Result<'msg, 'ignored>,
       uiBoundCmdParam: bool,
       wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    Binding.cmdParamIf (exec, uiBoundCmdParam)
    >> Binding.alterMsgStream wrapDispatch


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
    Binding.SubModelWin.create
      (fun m -> getState m |> WindowState.map (fun sub -> toBindingModel (m, sub)))
      (fun args -> DynamicViewModel<'bindingModel, 'bindingMsg>(args, bindings ()))
      IViewModel.updateModel
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
    Binding.SubModelWin.create
      (fun m -> getState m |> WindowState.map (fun sub -> (m, sub)))
      (fun args -> DynamicViewModel<'model * 'subModel, 'subMsg>(args, bindings ()))
      IViewModel.updateModel
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
    Binding.SubModelWin.create
      (fun m -> getState m |> WindowState.map (fun sub -> (m, sub)))
      (fun args -> DynamicViewModel<'model * 'subModel, 'msg>(args, bindings ()))
      IViewModel.updateModel
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
    Binding.SubModelSeqUnkeyed.create
      (fun args -> DynamicViewModel<'model, 'msg>(args, getBindings ()))
      IViewModel.updateModel

  static member subModelSeq // TODO: make into function
      (getBindings: unit -> Binding<'model, 'msg> list,
       getId: 'model -> 'id)
      : string -> Binding<'model seq, 'id * 'msg> =
    Binding.SubModelSeqKeyed.create
      (fun args -> DynamicViewModel<'model, 'msg>(args, getBindings ()))
      IViewModel.updateModel
      getId
      (IViewModel.currentModel >> getId)


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
    Binding.SubModelSeqKeyed.create
      (fun args -> DynamicViewModel<'bindingModel, 'bindingMsg>(args, bindings ()))
      IViewModel.updateModel
      getId
      (IViewModel.currentModel >> getId)
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
    Binding.SubModelSeqKeyed.create
      (fun args -> DynamicViewModel<'model * 'subModel, 'subMsg>(args, bindings ()))
      IViewModel.updateModel
      (snd >> getId)
      (IViewModel.currentModel >> snd >> getId)
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
    Binding.SubModelSeqKeyed.create
      (fun args -> DynamicViewModel<'model * 'subModel, 'msg>(args, bindings ()))
      IViewModel.updateModel
      (snd >> getId)
      (IViewModel.currentModel >> snd >> getId)
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
    Binding.SubModelSelectedItem.vopt subModelSeqBindingName
    >> Binding.addLazy (=)
    >> Binding.mapModel get
    >> Binding.mapMsgWithModel set
    >> Binding.addCaching

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
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
  static member subModelSelectedItem
      (subModelSeqBindingName: string,
       get: 'model -> 'id voption,
       set: 'id voption -> 'model -> 'msg,
       wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    Binding.subModelSelectedItem (subModelSeqBindingName, get, set)
    >> Binding.alterMsgStream wrapDispatch


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
    Binding.SubModelSelectedItem.opt subModelSeqBindingName
    >> Binding.addLazy (=)
    >> Binding.mapModel get
    >> Binding.mapMsgWithModel set
    >> Binding.addCaching

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
  /// <param name="wrapDispatch">
  ///   Wraps the dispatch function with additional behavior, such as
  ///   throttling, debouncing, or limiting.
  /// </param>
  [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
  static member subModelSelectedItem
      (subModelSeqBindingName: string,
       get: 'model -> 'id option,
       set: 'id option -> 'model -> 'msg,
       wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
      : string -> Binding<'model, 'msg> =
    Binding.subModelSelectedItem (subModelSeqBindingName, get, set)
    >> Binding.alterMsgStream wrapDispatch



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

    /// <summary>Creates a two-way binding.</summary>
    /// <param name="get">Gets the value from the model.</param>
    /// <param name="set">Returns the message to dispatch.</param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
    static member twoWay
        (get: 'model -> 'a,
         set: 'a -> 'msg,
         wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      Binding.twoWay (get, set)
      >> Binding.alterMsgStream wrapDispatch


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
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
    static member twoWayOpt
        (get: 'model -> 'a option,
         set: 'a option -> 'msg,
         wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      Binding.twoWayOpt (get, set)
      >> Binding.alterMsgStream wrapDispatch


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
    [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
    static member twoWayOpt
        (get: 'model -> 'a voption,
         set: 'a voption -> 'msg,
         wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      Binding.twoWayOpt (get, set)
      >> Binding.alterMsgStream wrapDispatch


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
    ///   Returns the validation messages from the updated model.
    /// </param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
    static member twoWayValidate
        (get: 'model -> 'a,
         set: 'a -> 'msg,
         validate: 'model -> string list,
         wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      Binding.twoWayValidate (get, set, validate)
      >> Binding.alterMsgStream wrapDispatch


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
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
    static member twoWayValidate
        (get: 'model -> 'a,
         set: 'a -> 'msg,
         validate: 'model -> string voption,
         wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      Binding.twoWayValidate (get, set, validate)
      >> Binding.alterMsgStream wrapDispatch


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
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
    static member twoWayValidate
        (get: 'model -> 'a,
         set: 'a -> 'msg,
         validate: 'model -> string option,
         wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      Binding.twoWayValidate (get, set, validate)
      >> Binding.alterMsgStream wrapDispatch


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
    [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
    static member twoWayValidate
        (get: 'model -> 'a,
         set: 'a -> 'msg,
         validate: 'model -> Result<'ignored, string>,
         wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      Binding.twoWayValidate (get, set, validate)
      >> Binding.alterMsgStream wrapDispatch


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
    ///   Returns the validation messages from the updated model.
    /// </param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
    static member twoWayOptValidate
        (get: 'model -> 'a voption,
         set: 'a voption -> 'msg,
         validate: 'model -> string list,
         wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      Binding.twoWayOptValidate (get, set, validate)
      >> Binding.alterMsgStream wrapDispatch


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
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
    static member twoWayOptValidate
        (get: 'model -> 'a voption,
         set: 'a voption -> 'msg,
         validate: 'model -> string voption,
         wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      Binding.twoWayOptValidate (get, set, validate)
      >> Binding.alterMsgStream wrapDispatch


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
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
    static member twoWayOptValidate
        (get: 'model -> 'a voption,
         set: 'a voption -> 'msg,
         validate: 'model -> string option,
         wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      Binding.twoWayOptValidate (get, set, validate)
      >> Binding.alterMsgStream wrapDispatch


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
    ///   Returns the validation message from the updated model.
    /// </param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
    static member twoWayOptValidate
        (get: 'model -> 'a voption,
         set: 'a voption -> 'msg,
         validate: 'model -> Result<'ignored, string>,
         wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      Binding.twoWayOptValidate (get, set, validate)
      >> Binding.alterMsgStream wrapDispatch


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
    ///   Returns the validation messages from the updated model.
    /// </param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
    static member twoWayOptValidate
        (get: 'model -> 'a option,
         set: 'a option -> 'msg,
         validate: 'model -> string list,
         wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      Binding.twoWayOptValidate (get, set, validate)
      >> Binding.alterMsgStream wrapDispatch


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
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
    static member twoWayOptValidate
        (get: 'model -> 'a option,
         set: 'a option -> 'msg,
         validate: 'model -> string voption,
         wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      Binding.twoWayOptValidate (get, set, validate)
      >> Binding.alterMsgStream wrapDispatch


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
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
    static member twoWayOptValidate
        (get: 'model -> 'a option,
         set: 'a option -> 'msg,
         validate: 'model -> string option,
         wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      Binding.twoWayOptValidate (get, set, validate)
      >> Binding.alterMsgStream wrapDispatch


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
    [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
    static member twoWayOptValidate
        (get: 'model -> 'a option,
         set: 'a option -> 'msg,
         validate: 'model -> Result<'ignored, string>,
         wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      Binding.twoWayOptValidate (get, set, validate)
      >> Binding.alterMsgStream wrapDispatch


    /// <summary>
    ///   Creates a <c>Command</c> binding that dispatches the specified message
    ///   and can always execute.
    /// </summary>
    /// <param name="exec">Returns the message to dispatch.</param>
    static member cmd
        (exec: 'msg)
        : string -> Binding<'model, 'msg> =
      Binding.Cmd.create
        (fun _ -> exec |> ValueSome)
        (fun _ -> true)

    /// <summary>
    ///   Creates a <c>Command</c> binding that dispatches the specified message
    ///   and can always execute.
    /// </summary>
    /// <param name="exec">Returns the message to dispatch.</param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
    static member cmd
        (exec: 'msg,
         wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      Binding.cmd exec
      >> Binding.alterMsgStream wrapDispatch


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
      Binding.Cmd.create
        (fun _ -> exec |> ValueSome)
        canExec

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
    [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
    static member cmdIf
        (exec: 'msg,
         canExec: 'model -> bool,
         wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      Binding.cmdIf (exec, canExec)
      >> Binding.alterMsgStream wrapDispatch


    /// <summary>
    ///   Creates a <c>Command</c> binding that depends on the
    ///   <c>CommandParameter</c>
    ///   and can always execute.
    /// </summary>
    /// <param name="exec">Returns the message to dispatch.</param>
    static member cmdParam
        (exec: obj -> 'msg)
        : string -> Binding<'model, 'msg> =
      Binding.Cmd.createWithParam
        (fun p _ -> exec p |> ValueSome)
        (fun _ _ -> true)
        false

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
    [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
    static member cmdParam
        (exec: obj -> 'msg,
         wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      Binding.cmdParam exec
      >> Binding.alterMsgStream wrapDispatch


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
      Binding.Cmd.createWithParam
        (fun p _ -> exec p)
        (fun p _ -> exec p |> ValueOption.isSome)
        (defaultArg uiBoundCmdParam false)

    /// <summary>
    ///   Creates a conditional <c>Command</c> binding that depends on the
    ///   <c>CommandParameter</c>
    ///   and can execute if <paramref name="exec" /> returns <c>ValueSome</c>.
    /// </summary>
    /// <param name="exec">Returns the message to dispatch.</param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
    static member cmdParamIf
        (exec: obj -> 'msg voption,
         wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      Binding.cmdParamIf exec
      >> Binding.alterMsgStream wrapDispatch

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
    [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
    static member cmdParamIf
        (exec: obj -> 'msg voption,
         uiBoundCmdParam: bool,
         wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      Binding.cmdParamIf (exec, uiBoundCmdParam)
      >> Binding.alterMsgStream wrapDispatch


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
      Binding.Cmd.createWithParam
        (fun p _ -> exec p |> ValueOption.ofOption)
        (fun p _ -> exec p |> Option.isSome)
        (defaultArg uiBoundCmdParam false)

    /// <summary>
    ///   Creates a conditional <c>Command</c> binding that depends on the
    ///   <c>CommandParameter</c>
    ///   and can execute if <paramref name="exec" /> returns <c>Some</c>.
    /// </summary>
    /// <param name="exec">Returns the message to dispatch.</param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
    static member cmdParamIf
        (exec: obj -> 'msg option,
         wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      Binding.cmdParamIf exec
      >> Binding.alterMsgStream wrapDispatch

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
    [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
    static member cmdParamIf
        (exec: obj -> 'msg option,
         uiBoundCmdParam: bool,
         wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      Binding.cmdParamIf (exec, uiBoundCmdParam)
      >> Binding.alterMsgStream wrapDispatch


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
      Binding.Cmd.createWithParam
        (fun p _ -> exec p |> ValueOption.ofOk)
        (fun p _ -> exec p |> Result.isOk)
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
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
    static member cmdParamIf
        (exec: obj -> Result<'msg, 'ignored>,
         wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      Binding.cmdParamIf exec
      >> Binding.alterMsgStream wrapDispatch

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
    [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
    static member cmdParamIf
        (exec: obj -> Result<'msg, 'ignored>,
         uiBoundCmdParam: bool,
         wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      Binding.cmdParamIf (exec, uiBoundCmdParam)
      >> Binding.alterMsgStream wrapDispatch


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
      Binding.Cmd.createWithParam
        (fun p _ -> exec p |> ValueSome)
        (fun p _ -> canExec p)
        (defaultArg uiBoundCmdParam false)

    /// <summary>
    ///   Creates a <c>Command</c> binding that depends on the
    ///   <c>CommandParameter</c>
    ///   and can execute if <paramref name="canExec" /> returns <c>true</c>.
    /// </summary>
    /// <param name="exec">Returns the message to dispatch.</param>
    /// <param name="canExec">Indicates whether the command can execute.</param>
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
    static member cmdParamIf
        (exec: obj -> 'msg,
         canExec: obj -> bool,
         wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      Binding.cmdParamIf (exec, canExec)
      >> Binding.alterMsgStream wrapDispatch

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
    [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
    static member cmdParamIf
        (exec: obj -> 'msg,
         canExec: obj -> bool,
         uiBoundCmdParam: bool,
         wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      Binding.cmdParamIf (exec, canExec, uiBoundCmdParam)
      >> Binding.alterMsgStream wrapDispatch


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
      Binding.SubModelSelectedItem.vopt subModelSeqBindingName
      >> Binding.addLazy (=)
      >> Binding.mapModel get
      >> Binding.mapMsg set
      >> Binding.addCaching

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
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
    static member subModelSelectedItem
        (subModelSeqBindingName: string,
         get: 'model -> 'id voption,
         set: 'id voption -> 'msg,
         wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      Binding.subModelSelectedItem (subModelSeqBindingName, get, set)
      >> Binding.alterMsgStream wrapDispatch


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
      Binding.SubModelSelectedItem.opt subModelSeqBindingName
      >> Binding.addLazy (=)
      >> Binding.mapModel get
      >> Binding.mapMsg set
      >> Binding.addCaching

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
    /// <param name="wrapDispatch">
    ///   Wraps the dispatch function with additional behavior, such as
    ///   throttling, debouncing, or limiting.
    /// </param>
    [<System.Obsolete("In version 5, this method will be removed.  Use the overload without the \"wrapDispatch\" parameter followed by a call to \"Binding.alterMsgStream\".  For an example, see how this method is implemented.")>]
    static member subModelSelectedItem
        (subModelSeqBindingName: string,
         get: 'model -> 'id option,
         set: 'id option -> 'msg,
         wrapDispatch: Dispatch<'msg> -> Dispatch<'msg>)
        : string -> Binding<'model, 'msg> =
      Binding.subModelSelectedItem (subModelSeqBindingName, get, set)
      >> Binding.alterMsgStream wrapDispatch
