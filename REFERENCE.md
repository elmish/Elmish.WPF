Elmish.WPF Reference
====================

Table of contents
-----------------

* [The Elmish.WPF bindings](#the-elmishwpf-bindings)
  + [One-way bindings](#one-way-bindings)
    - [Binding to option-wrapped values](#binding-to-option-wrapped-values)
  + [Two-way bindings](#two-way-bindings)
    - [Binding to option-wrapped values](#binding-to-option-wrapped-values-1)
    - [Using validation with two-way bindings](#using-validation-with-two-way-bindings)
  + [Command bindings](#command-bindings)
    - [Conditional commands (where you control `CanExecute`)](#conditional-commands-where-you-control-canexecute)
    - [Using the `CommandParameter`](#using-the-commandparameter)
  + [Sub-model bindings](#sub-model-bindings)
    - [Level 1: No separate message type or customization of model for sub-bindings](#level-1-no-separate-message-type-or-customization-of-model-for-sub-bindings)
    - [Level 2: Separate message type but no customization of model for sub-bindings](#level-2-separate-message-type-but-no-customization-of-model-for-sub-bindings)
    - [Level 3: Separate message type and arbitrary customization of model for sub-bindings](#level-3-separate-message-type-and-arbitrary-customization-of-model-for-sub-bindings)
    - [Optional and “sticky” sub-model bindings](#optional-and-sticky-sub-model-bindings)
  + [Sub-model window bindings](#sub-model-window-bindings)
  + [Sub-model sequence bindings](#sub-model-sequence-bindings)
  + [Other bindings](#other-bindings)
    - [`subModelSelectedItem`](#submodelselecteditem)
    - [`oneWaySeq`](#onewayseq)
* [Modifying bindings](#modifying-bindings)
  + [Lazy bindings](#lazy-bindings)
  + [Mapping bindings](#mapping-bindings)
    - [Example use of `mapModel` and `mapMsg`](#example-use-of-mapModel-and-mapMsg)
    - [Theory behind `mapModel` and `mapMsg`](#theory-behind-mapModel-and-mapMsg)


The Elmish.WPF bindings
----------------------------

The Elmish.WPF bindings can be categorized into the following types:

- **One-way bindings**, for when you want to bind to a simple value.
- **Two-way bindings**, for when you want to bind to a simple value as well as update this value by dispatching a message. Used for inputs, checkboxes, sliders, etc. Can optionally support validation (e.g. provide an error message using `INotifyDataErrorInfo` that can be displayed when an input is not valid).
- **Command bindings**, for when you want a message to be dispatched when something happens (e.g. a button is clicked).
- **Sub-model bindings**, for when you want to bind to a complex object that has its own bindings.
- **Sub-model window bindings**, for when you want to control the opening/closing/hiding of new windows.
- **Sub-model sequence bindings**, for when you want to bind to a collection of complex objects, each of which has its own bindings.
- **Other bindings** not fitting into the categories above
- **Lazy bindings**, optimizations of various other bindings that allow skipping potentially expensive computations if the input is unchanged

Additionally, there is a section explaining how most dispatching bindings allow you to wrap the dispatcher to support debouncing/throttling etc.

### One-way bindings

*Relevant sample: SingleCounter - ([XAML views](src/Samples/SingleCounter) and [F# core](src/Samples/SingleCounter.Core))*

One-way bindings are used when you want to bind to a simple value.

In the counter example mentioned previously, the binding to the counter value is a one-way binding:

```f#
"CounterValue" |> Binding.oneWay (fun m -> m.Count)
```

In XAML, the binding can look like this:

```xaml
<TextBlock Text="{Binding CounterValue, StringFormat='Counter value: {0}'}" />
```

A one-way binding simply accepts a function `get: 'model -> 'a` that retrieves the value to be displayed.

#### Binding to option-wrapped values

In F#, it’s common to model missing values using the `Option` type. However, WPF uses `null` and doesn’t know how to handle the F# `Option` type. You could simply convert from `Option` to `null` (or `Nullable<_>`) in the `get` function using `Option.toObj` (or `Option.toNullable`), but this is such a common scenario that Elmish.WPF has a variant of the one-way binding called `oneWayOpt` with this behavior built-in. The `oneWayOpt` binding accepts a function `get: 'model -> 'a option`. If it returns `None`, the UI will receive `null`. If it returns `Some`, the UI will receive the inner value.

### Two-way bindings

*Relevant sample: SingleCounter - ([XAML views](src/Samples/SingleCounter) and [F# core](src/Samples/SingleCounter.Core))*

Two-way bindings are commonly used for any kind of input (textboxes, checkboxes, sliders, etc.). The two-way bindings accept two functions: A function `get: 'model -> 'a` just like the one-way binding, and a function `set: 'a -> 'model -> 'msg ` that accepts the UI value to be set and the current model, and returns the message to be dispatched.

In the counter example above, the two-way binding to the slider value may look like this:

```f#
"StepSize" |> Binding.twoWay(
  (fun m -> float m.StepSize),
  (fun v m -> SetStepSize (int v))
)
```

The corresponding XAML may look like this:

```f#
<Slider
  Value="{Binding StepSize}"
  TickFrequency="1"
  Minimum="1"
  Maximum="10"
  IsSnapToTickEnabled="True" />
```

The WPF slider’s value is a `float`, but in the model we use an `int`. Therefore the binding’s `get` function must convert the model’s integer to a float, and conversely, the binding’s “setter” must convert the UI value from a float to an int.

You might think that the `get` function doesn’t have to cast to `float`. However, `'a` is the same in both `get` and `set`, and if you return `int` in `get`, then Elmish.WPF expects the value coming from the UI (which is `obj`) to also be `int`, and will try to unbox it to `int` when being set. Since it actually is a `float`, this will fail.

It’s common for the `set` function to rely only on the value to be set, not on the model. Therefore, the two-way binding also has an overload where the `set` function accepts only the value, not the model. This allows a more shorthand notation:

```f#
"StepSize" |> Binding.twoWay(
  (fun m -> float m.StepSize),
  (int >> SetStepSize)
)
```

#### Binding to option-wrapped values

Just like one-way bindings, there is a variant of the two-way binding for `option`-wrapped values. The `option` wrapping is used in both `get` and `set`. Elmish.WPF will convert both ways between a possibly `null` raw value and an `option`-wrapped value.

#### Using validation with two-way bindings

*Relevant sample: Validation - ([XAML views](src/Samples/Validation) and [F# core](src/Samples/Validation.Core))*

You might want to display validation errors when the input is invalid. The best way to do this in WPF is through `INotifyDataErrorInfo`. Elmish.WPF supports this directly through the `twoWayValidate` bindings. In addition to `get` and `set`, this binding also accepts a third parameter that returns the error string to be displayed. This can be returned as `string option` (where `None` indicates no error), or `Result<_, string>` (where `Ok` indicates no error; this variant might allow you to easily reuse existing validation functions you have).

Keep in mind that by default, WPF controls do not display errors. To display errors, either use 3rd party controls/styles (such as [MaterialDesignInXamlToolkit](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit)) or add your own styles (the `Validation` sample in this repo demonstrates this).

There are also variants of the two-way validating bindings for option-wrapped values.

### Command bindings

*Relevant sample: SingleCounter - ([XAML views](src/Samples/SingleCounter) and [F# core](src/Samples/SingleCounter.Core))*

Command bindings are used whenever you use `Command`/`CommandParameter` in XAML, such as for button clicks.

For example, for the counter app we have been looking at, the XAML binding to execute a command when the “Increment” button is clicked might look like this:

```xaml
<Button Command="{Binding Increment}" Content="+" />
```

The corresponding Elmish.WPF binding that dispatches `Msg.Increment` when the command is executed generally looks like this:

```f#
"Increment" |> Binding.cmd (fun m -> Increment)
```

The binding accepts a single function `exec: 'model -> 'msg ` that accepts the current model and returns the message to be dispatched. Elmish.WPF will convert the message to an `ICommand` that dispatches the message when the command is invoked.

For convenience, if you don’t need the model, there is also an overload that directly accepts the message (instead of a model-accepting function). The above can therefore be written like this:

```f#
"Increment" |> Binding.cmd Increment
```

#### Conditional commands (where you control `CanExecute`)

*Relevant sample: SingleCounter - ([XAML views](src/Samples/SingleCounter) and [F# core](src/Samples/SingleCounter.Core))*

A command may not always be executable. As you might know, WPF’s `ICommand` interface contains a `CanExecute` method that, if `false`, will cause WPF to disable the bound control (e.g. the button).

In the counter example, we might want to prohibit negative numbers, disabling the `Decrement` button when the `model.Count = 0`. This can be written using `cmdIf`:

```f#
"Decrement" |> Binding.cmdIf (
  (fun m -> Decrement),
  fun m -> m.Count >= 0
)
```

There are several ways to indicate that a command can‘t execute. The `cmdIf` binding has overloads for the following:

- `exec: 'model -> 'msg option`, where the command is disabled if `exec` returns `None`
- `exec: 'model -> Result<'msg, _>`, where the command is disabled if `exec` returns `Error`
- `exec: 'model  -> 'msg * canExec: 'model -> bool` (as the example above shows), where the command is disabled if `canExec` returns `false` (and as with `cmd`, there is also an overload where `exec` is simply the message to dispatch)

#### Using the `CommandParameter`

*Relevant sample: UiBoundCmdParam - ([XAML views](src/Samples/UiBoundCmdParam) and [F# core](src/Samples/UiBoundCmdParam.Core))*

There may be times you need to use the XAML `CommandParameter` property. You then need to use Elmish.WPF’s `cmdParam` binding, which works exactly like `cmd` but where `exec` function accepts the command parameter as its first parameter.

There is also `cmdParamIf` which combines `cmdParam` and `cmdIf`, allowing you to override the command’s `CanExecute`.

### Sub-model bindings

*Relevant sample: SubModel - ([XAML views](src/Samples/SubModel) and [F# core](src/Samples/SubModel.Core))*

Sub-model bindings are used when you want to bind to a complex object that has its own bindings. In MVVM, this happens when one of your view-model properties is another view model with its own properties the UI can bind to.

Perhaps the most compelling use-case for sub-models is when binding the `ItemsSource` of a `ListView` or similar. Each item in the collection you bind to is a view-model with its own properties that is used when rendering each item. However, the same principles apply when there’s only a single sub-model. Collections are treated later; this section focuses on a single sub-model.

The `subModel` binding has three overloads, increasing in complexity depending on how much you need to customize the sub-bindings.

#### Level 1: No separate message type or customization of model for sub-bindings

This is sufficient for many purposes. The overload accepts two parameters:

- `getSubModel: 'model -> 'subModel` to obtain the sub-model
- `bindings: unit -> Binding<'model * 'subModel, 'msg> list`, the bindings for the sub-model

In other words, inside the sub-bindings, the model parameter (in each binding) is a tuple with the parent model and the sub-model.

For example, let’s say that we have an app where a counter is a part of the app. We might do this:

```f#
"Counter" |> Binding.subModel(
	(fun m -> m.Counter),  // Counter is an object with Count, StepSize, etc.
  (fun () -> [
  	"CounterValue" |> Binding.oneWay (fun (parent, counter) -> counter.Count)
  	"Increment" |> Binding.cmd IncrementCounter
  ])
)
```

As you can see, inside the sub-bindings (which could be extracted to their own `bindings` function), the model parameter is a tuple containing the parent state as well as the sub-model state. This is a good default because it’s the most general signature, allowing you access to everything from the parent as well as the sub-model you are binding to. (This is particularly important for sub-model sequence bindings, which are described later.)

Note also that the sub-bindings still use the top-level message type. There is no separate child message type for the sub-model; `IncrementCounter` is a case of the parent message type. This is also a good default for the reasons described in the earlier “child components and scaling” section.

#### Level 2: Separate message type but no customization of model for sub-bindings

This overload is just like the first one except it has an additional parameter to transform the message type:

- `getSubModel: 'model -> 'subModel` to obtain the sub-model
- `toMsg: 'subMsg -> 'msg` to wrap the child message in a parent message
- `bindings: unit -> Binding<'model * 'subModel, 'subMsg> list`, the bindings for the sub-model

This is useful if you want to use a separate message type in the sub-model bindings. For the `toMsg` parameter, you would typically pass a parent message case that wraps the child message type. For example:

```f#
"Counter" |> Binding.subModel(
	(fun m -> m.Counter),
	CounterMsg,
  (fun () -> [
  	"CounterValue" |> Binding.oneWay (fun (parent, counter) -> counter.Count)
  	"Increment" |> Binding.cmd Increment
  ])
)
```

Here,  `Increment` is a case of the child message type, and `CounterMsg` is a parent message case that wraps the counter message type.

If you had passed `id` as the `toMsg` parameter, you would have the same behavior as the previous simpler overload with no `toMsg`.

#### Level 3: Separate message type and arbitrary customization of model for sub-bindings

This is the most complex one, and is required for the following cases:

- recursive models
- proper “child components” with their own model/update/bindings unrelated to their parent

The reasons it’s required for these cases are described further below.

It’s also nice to have if you simply want to “clean up” or otherwise customize the model used in the bindings (e.g. if you don’t need the parent model, only the child model).

Compared to the “level 2” overload, it has one additional parameter, `toBindingModel`. All the parameters are:

- `getSubModel: 'model -> 'subModel` to obtain the sub-model
- `toBindingModel: ('model * 'subModel) -> 'bindingModel` to transform the default binding model to whatever you want
- `toMsg: 'bindingMsg -> 'msg` to wrap the message used in the sub-model bindings in a parent message
- `bindings: unit -> Binding<'bindingModel, 'bindingMsg> list`, the bindings for the sub-model

Continuing with the counter example above, it could look like this:

```f#
"Counter" |> Binding.subModel(
	(fun m -> m.Counter),
	(fun (parent, counter) -> counter)
	CounterMsg,
  (fun () -> [
  	"CounterValue" |> Binding.oneWay (fun counter -> counter.Count)
  	"Increment" |> Binding.cmd Increment
  ])
)
```

As you see, we transform the default `(parent, counter)` tuple into just the `counter`, so that the model used in the sub-bindings is only the `'subModel`. Otherwise the example is the same. If you had passed `id` to `toBindingModel` and `toMsg`, you would end up with the same behavior as the simplest variant without `toBindingModel` and `toMsg`.

The model transformation allowed by this overload is required for a proper, separate “child component” with its own model/message/bindings, because the child component’s bindings would of course not know anything about any parent model. I.e., as demonstrated above, you need the model to be just `'subModel` and not `'model * 'subModel`

The model transformation is also required for recursive bindings. Imagine that a counter can contain another counter (in a `ChildCounter` property). You would define the (recursive) counter bindings as:

```f#
let rec counterBindings () : Binding<CounterModel, CounterMsg> list = [
  	"CounterValue" |> Binding.oneWay (fun m -> m.Count)
  	"Increment" |> Binding.cmd Increment
  	"ChildCounter" |> Binding.subModel(
      (fun m -> m.ChildCounter),
      (fun (parent, counter) -> counter)
      ChildMsg,
      counterBindings
  ])
```

If you could not transform `(parent, counter)` “back” to `counter`, you could not reuse the same bindings, and hence not create recursive bindings.

Recursive bindings are demonstrated in the `SubModelSeq` sample.

You now have the power to create child components. Use it with great care; as mentioned in the earlier “child components and scaling” section, such separation will often do more harm than good.

#### Optional and “sticky” sub-model bindings

*Relevant sample: SubModelOpt - ([XAML views](src/Samples/SubModelOpt) and [F# core](src/Samples/SubModelOpt.Core))*

You can also use the `subModelOpt` binding. The signature is the same as the variants described above, except that `getSubModel` returns `'subModel option`. The UI will receive `null` when the sub-model is `None`.

Additionally, these bindings have an optional `sticky: bool` parameter. If `true`, Elmish.WPF will “remember” and return the most recent non-null sub-model when the `getSubModel` returns `None`. This can be useful for example when you want to animate away the UI for the sub-component when it’s set to `None`. If you do not use `sticky`, the UI will be cleared at the start of the animation, which may look weird.

### Sub-model window bindings

*Relevant sample: NewWindow - ([XAML views](src/Samples/NewWindow) and [F# core](src/Samples/NewWindow.Core))*

The `subModelWin` binding is a variant of `subModelOpt` that allows you to control the opening/closing/hiding of new windows. It has the same overloads as `subModel` and `subModelOpt`, with two key differences: First, the sub-model is wrapped in a custom type called `WindowState` that is defined like this:

```f#
[<RequireQualifiedAccess>]
type WindowState<'model> =
  | Closed
  | Hidden of 'model
  | Visible of 'model
```

By wrapping the sub-model in `WindowState.Hidden` or `WindowState.Visible` or returning `WindowState.Closed`, you control the opening, closing, showing, and hiding of a window whose `DataContext` will be automatically set to the wrapped model. Check out the `NewWindow` sample to see it in action.

Secondly, all overloads have the following parameter:

```f#
getWindow: 'model -> Dispatch<'msg> -> #Window
```

This is what’s actually called to create the window. You have access to the current model as well as the `dispatch` in case you need to set up message-dispatching event subscriptions for the window.

Additionally, all `subModelWin` overloads have two optional parameters. The first is `?onCloseRequested: 'msg`. Returning `WindowState.Closed` is the only way to close the window. In order to support closing using external mechanisms (the Close/X button, Alt+F4, or System Menu -> Close), this parameter allows you to specify a message that will be dispatched for these events. You can then react to this message by updating your state so that the binding returns `WindowState.Closed`

The second optional parameter is `?isModal: bool`. This specifies whether the window will be shown modally (using `window.ShowDialog`, blocking the rest of the UI) or non-modally (using `window.Show`).

Again, check out the `NewWindow` sample to see `subModelWin` in action.

### Sub-model sequence bindings

*Relevant sample: SubModelSeq - ([XAML views](src/Samples/SubModelSeq) and [F# core](src/Samples/SubModelSeq.Core))*

If you understand `subModel`, then `subModelSeq` isn’t much more complex. It has similar overloads, but instead of returning a single sub-model, you return `#seq<'subModel>`. Furthermore, all overloads have an additional parameter `getId` (which for the “level 1” and “level 2” overloads has signature `'subModel -> 'id`) that gets a unique identifier for each model. This identifier must be unique among all sub-models in the collection, and is used to know which items to add, remove, re-order, and update.

The `toMsg` parameter in the “level 2” and “level 3” overloads has the signature `'id * 'subMsg -> 'msg` (compared with just `'subMsg -> 'msg` for `subModel`). For this parameter you would typically use a parent message case that wraps both the child ID and the child message. You need the ID to know which sub-model it came from, and thus which sub-model to pass the message along to.

Finally, in the “level 3” overload that allows you to transform the model used for the bindings, the `getId` parameter has signature `'bindingModel -> 'id` (instead of `'subModel -> 'id` for the two simpler overloads).

### Other bindings

There are two special bindings not yet covered.

#### `subModelSelectedItem`

*Relevant sample: SubModelSelectedItem - ([XAML views](src/Samples/SubModelSelectedItem) and [F# core](src/Samples/SubModelSelectedItem.Core))*

The section on model normalization made it clear that it’s better to use IDs than complex objects in messages. This means that for bindings to the selected value of a `ListBox` or similar, you’ll likely have better luck using `SelectedValue` and `SelectedValuePath` rather than `SelectedItem`.

Unfortunately some selection-enabled WPF controls only have `SelectedItem` and do not support `SelectedValue` and `SelectedValuePath`. Using `SelectedItem` is particularly cumbersome in Elmish.WPF since the value is not your sub-model, but an instance of the Elmish.WPF view-model. To help with this, Elmish.WPF provides the `subModelSelectedItem` binding.

This binding works together with a `subModelSeq` binding in the same binding list, and allows you to use the `subModelSeq` binding’s IDs in your model while still using `SelectedItem` from XAML. For example, if you use `subModelSeq` to display a list of books identified by a `BookId`, the `subModelSelectedItem` binding allows you to use `SelectedBook: BookId` in your model.

The `subModelSelectedItem` binding has the following parameters:

- `subModelSeqBindingName: string`, where you identify the binding name for the corresponding `subModelSeq` binding
- `get: 'model -> 'id option`, where you return the ID of the sub-model in the `subModelSeq` binding that should be selected
- `set: 'id option -> 'msg`, where you return the message to dispatch when the selected item changes (typically this will be a message case wrapping the ID).

You bind the `SelectedItem` of a control to the `subModelSelectedItem` binding. Then, Elmish.WPF will take care of the following:

- When the UI retrieves the selected item, Elmish.WPF gets the ID using `get`, looks up the correct view-model in the `subModelSeq` binding identified by `subModelSeqBindingName`, and returns that view-model to the UI.
- When the UI sets the selected item (which it sets to an Elmish.WPF view-model), Elmish.WPF calls `set` with the ID of the sub-model corresponding to that view-model.

#### `oneWaySeq`

*Relevant sample: OneWaySeq - ([XAML views](src/Samples/OneWaySeq) and [F# core](src/Samples/OneWaySeq.Core))*

In some cases, you might want to have a one-way binding not to a single, simple value, but to a potentially large collection of simple values. If you use `oneWay` for this, the entire list will be replaced and re-rendered each time the model updates.

In the special case that you want to bind to a collection of **simple** (can be bound to directly) and **distinct** values, you can use `oneWaySeq`. This will ensure that only changed items are replaced/moved.

The `oneWaySeq` binding has the following parameters:

- `get: 'model -> #seq<'a>`, to retrieve the collection
- `itemEquals: 'a -> 'a -> bool`, to determine whether an item has changed
- `getId: 'a -> 'id`, to track which items are added, removed, re-ordered, and changed

If the values are not simple (e.g. not strings or numbers), then you can instead use `subModelSeq` to set up separate bindings for each item. And if the values are not distinct (i.e., can not be uniquely identified in the collection), then Elmish.WPF won’t be able to track which items are moved, and you can’t use this optimization.

Note that you can always use `subModelSeq` instead of `oneWaySeq` (the opposite is not true.) The `oneWaySeq` binding is slightly simpler than `subModelSeq` if the elements are simple values that can be bound to directly.

Modifying bindings
------------------

### Lazy bindings

*Note: Lazy bindings may get a complete overhaul soon; see [#143](https://github.com/elmish/Elmish.WPF/issues/143).*

You may find yourself doing potentially expensive work in one-way bindings. To facilitate simple optimization in these cases, Elmish.WPF provides the bindings `oneWayLazy`, `oneWayOptLazy`, and `oneWaySeqLazy`. The difference between these and their non-lazy counterparts is that they have two extra parameters: `equals` and `map `.

The optimization is done at two levels. The first optimization is for the update process. As with the non-lazy bindings, the initial `get` function is called. For the lazy bindings, this should be cheap; it should basically just return what you need from from the model (e.g. a single item or a tuple or record with multiple items). Then, `equals` is used to compare the output of `get` with the previous output of `get`. If `equals` returns `true`, the rest of the update process is skipped entirely. If `equals` returns `false`, the output of `get` is passed to `map`, which may be expensive, and then the binding is updated normally.

The second optimization is when the UI retrieves the value. The output of `map` is cached, so if the UI attempts to retrieve a value multiple times, `map` is still only called once. Contrast this the non-lazy bindings, where `get` is called each time the value is retrieved by the UI.

Elmish.WPF provides two helpers you can often use as the `equals` parameter: `refEq` and `elmEq`.

- `refEq` is a good choice if `get` returns a single item (not an inline-created tuple, record, or other wrapper) from your model. It is simply an alias for `LanguagePrimitives.PhysicalEquality` (which is essentially `Object.ReferenceEquals` with better typing). Since the Elmish model is generally immutable, a reference equality check for the output of `get` is a very efficient way to short-circuit the update process. It may cause false negatives if two values are structurally equal but not referentially equal, but this should not be a common case, and structural equality may be prohibitively expensive if comparing e.g. large lists, defeating the purpose.
- `elmEq` is a good choice if `get` returns multiple items from the model wrapped inline in a tuple or record. It will compare each member of the `get` return value separately (i.e. each record field, or each tuple item). Reference-typed members will be compared using reference equality, and string members and value-typed members will be compared using structural equality.

You may pass any function you want for `equals`; it does not have to be one of the above. For example, if you want structural comparison (note the caveat above however), you can pass `(=)`.

### Mapping Bindings

Sometimes duplicate mapping code exists across several bindings. The duplicate mappings could be from the parent model to a common child model or it could be the wrapping of a child message in a parent message, which might depend on the parent model. The duplicate mapping code can be extracted and written once using the mapping functions `mapModel`, `mapMsg`, and `mapMsgWithModel`.

#### Example use of `mapModel` and `mapMsg`

Here is a simple example that uses these model and message types.
```F#
type ChildModel =
  { GrandChild1: GrandChild1
    GrandChild2: GrandChild2 }

type ChildMsg =
  | SetGrandChild1 of GrandChild1
  | SetGrandChild2 of GrandChild2

type ParentModel =
  { Child: ChildModel }

type ParentMsg =
  | ChildMsg of ChildMsg
```

It is possible to create bindings from the parent to the two grandchild fields, but there is duplicate mapping code.

```F#
let parentBindings () : Binding<ParentModel, ParentMsg> list = [
  "GrandChild1" |> Binding.twoWay((fun parent -> parent.Child.GrandChild1), SetGrandChild1 >> ChildMsg)
  "GrandChild2" |> Binding.twoWay((fun parent -> parent.Child.GrandChild2), SetGrandChild2 >> ChildMsg)
]
```

The functions `mapModel` and `mapMsg` can remove this duplication.
```F#
let childBindings () : Binding<ChildModel, ChildMsg> list = [
  "GrandChild1" |> Binding.twoWay((fun child -> child.GrandChild1), SetGrandChild1)
  "GrandChild2" |> Binding.twoWay((fun child -> child.GrandChild2), SetGrandChild2)
]

let parentBindings () : Binding<ParentModel, ParentMsg> list =
  childBindings ()
  |> Bindings.mapModel (fun parent -> parent.Child)
  |> Bindings.mapMsg ChildMsg
```

#### Benefit for design-time view models

With such duplicate mapping code extracted, it is easier to create a design-time view model for the XAML code containing the bindings to `GrandChild1` and `GrandChild2`.  Specifically, instead of creating the design-time view model from the `parentBindings` bindings, it can now be created from the `childBindings` bindings.  The `SubModelSeq` sample uses this benefit to create a design-time view model for `Counter.xaml`.

#### Theory behind `mapModel` and `mapMsg`

A binding in Elmish.WPF is represented by an instance of type `Binding<'model, 'msg>`. It is a profunctor, which means that
- it is a contravariant functor in `'model` with `mapModel` as the corresponding mapping function for this functor and
- it is a covariant functor in `'msg` with `mapMsg` as the corresponding mapping function for this functor.
