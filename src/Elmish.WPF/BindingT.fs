namespace Elmish.WPF

open System.Windows

open Elmish


module BindingT =
  let internal createStatic data name = { DataT = data; Name = name }
  open BindingData

  let internal mapData f binding =
    { DataT = binding.DataT |> f
      Name = binding.Name }

  /// Map the model of a binding via a contravariant mapping.
  let mapModel (f: 'a -> 'b) (binding: BindingT<'b, 'msg, 'vm>) = f |> mapModel |> mapData <| binding

  /// Map the message of a binding with access to the model via a covariant mapping.
  let mapMsgWithModel (f: 'a -> 'model -> 'b) (binding: BindingT<'model, 'a, 'vm>) = f |> mapMsgWithModel |> mapData <| binding

  /// Map the message of a binding via a covariant mapping.
  let mapMsg (f: 'a -> 'b) (binding: BindingT<'model, 'a, 'vm>) = f |> mapMsg |> mapData <| binding

  /// Set the message of a binding with access to the model.
  let setMsgWithModel (f: 'model -> 'b) (binding: BindingT<'model, 'a, 'vm>) = f |> setMsgWithModel |> mapData <| binding

  /// Set the message of a binding.
  let setMsg (msg: 'b) (binding: BindingT<'model, 'a, 'vm>) = msg |> setMsg |> mapData <| binding


  /// Restrict the binding to models that satisfy the predicate after some model satisfies the predicate.
  let addSticky (predicate: 'model -> bool) (binding: BindingT<'model, 'msg, 'vm>) = predicate |> addSticky |> mapData <| binding

  /// <summary>
  ///   Adds caching to the given binding.  The cache holds a single value and
  ///   is invalidated after the given binding raises the
  ///   <c>PropertyChanged</c> event.
  /// </summary>
  /// <param name="binding">The binding to which caching is added.</param>
  let addCaching (binding: BindingT<'model, 'msg, 'vm>) : BindingT<'model, 'msg, 'vm> =
    binding
    |> mapData addCaching

  /// <summary>
  ///   Adds validation to the given binding using <c>INotifyDataErrorInfo</c>.
  /// </summary>
  /// <param name="validate">Returns the errors associated with the given model.</param>
  /// <param name="binding">The binding to which validation is added.</param>
  let addValidation (validate: 'model -> string list) (binding: BindingT<'model, 'msg, 'vm>) : BindingT<'model, 'msg, 'vm> =
    binding
    |> (addValidation validate |> mapData)

  /// <summary>
  ///   Adds laziness to the updating of the given binding. If the models are considered equal,
  ///   then updating of the given binding is skipped.
  /// </summary>
  /// <param name="equals">Updating skipped when this function returns <c>true</c>.</param>
  /// <param name="binding">The binding to which the laziness is added.</param>
  let addLazy (equals: 'model -> 'model -> bool) (binding: BindingT<'model, 'msg, 'vm>) : BindingT<'model, 'msg, 'vm> =
    binding
    |> (addLazy equals |> mapData)

  /// <summary>
  ///   Accepts a function that can alter the message stream.
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
  /// <param name="alteration">The function that will alter the message stream.</param>
  /// <param name="binding">The binding to which the message stream is altered.</param>
  let alterMsgStream (alteration: ('b -> unit) -> 'a -> unit) (binding: BindingT<'model, 'a, 'vm>) : BindingT<'model, 'b, 'vm> =
    binding
    |> (alterMsgStream alteration |> mapData)

  module Get =
    open BindingData.OneWay

    let id<'a, 'msg> =
      id<'a, 'msg>
      |> createStatic

    let get get =
      id
      >> mapModel get


  module Set =
    open BindingData.OneWayToSource
    
    let id<'model, 'a> =
      id<'model, 'a>
      |> createStatic

  module Cmd =
    open BindingData.Cmd

    let createWithParam exec canExec autoRequery =
      createWithParam exec canExec autoRequery
      |> createStatic

  module SubModel =
    open BindingData.SubModel

    let opt<'bindingModel, 'msg, 'viewModel when 'viewModel :> ISubModel<'bindingModel, 'msg>> createVm : StaticBindingT<'bindingModel voption, 'msg, 'viewModel> =
      create createVm (fun ((vm: 'viewModel),m) -> vm.StaticHelper.UpdateModel(m))
      |> createStatic

    let req<'bindingModel, 'msg, 'viewModel when 'viewModel :> ISubModel<'bindingModel, 'msg>> createVm : StaticBindingT<'bindingModel, 'msg, 'viewModel> =
      create createVm (fun ((vm: 'viewModel),m) -> vm.StaticHelper.UpdateModel(m))
      |> createStatic
      >> mapModel ValueSome


[<AllowNullLiteral>]
type InnerExampleViewModel(args) as this =
  interface ISubModel<string, int64> with
    member _.StaticHelper = StaticHelper.create args (fun () -> this)
    
[<AllowNullLiteral>]
type ExampleViewModel(args) as this =
  let staticHelper = StaticHelper.create args (fun () -> this)

  member _.Model
    with get() = BindingT.Get.id |> staticHelper.Get()
    and set(v) = BindingT.Get.id >> BindingT.mapMsg int32<string> |> staticHelper.Set(v)
  member _.Command = BindingT.Cmd.createWithParam (fun _ _ -> ValueNone) (fun _ _ -> true) false |> staticHelper.Get()
  member _.SubModel = BindingT.SubModel.opt InnerExampleViewModel >> BindingT.mapModel ValueSome >> BindingT.mapMsg int32 |> staticHelper.Get()
