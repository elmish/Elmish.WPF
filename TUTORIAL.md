Elmish.WPF Tutorial
===================

<img src="https://raw.githubusercontent.com/elmish/Elmish.WPF/master/logo/elmish-wpf-logo-ghreadme.png" width="300" align="right" />

The aim of this tutorial is to explain how to use Elmish.WPF, building in complexity from start (what is MVU?) to end (using complex bindings and applying optimizations).

This tutorial is not directly related to the many samples in the Elmish.WPF repository, but complements them well. The samples are complete, fully functional apps demonstrating selected aspects of Elmish.WPF. The samples *show*; the tutorial *explains*.

This tutorial assumes working F# knowledge. If you’re new to F#, Scott Wlaschin’s blog [F# for fun and profit](https://fsharpforfunandprofit.com/) is a great place to start (and continue) learning the ins and outs of F# and functional programming. His book [Domain Modeling Made Functional](https://pragprog.com/book/swdddf/domain-modeling-made-functional) is also a great resource for learning F# (and in particular how it can be used for domain modeling). You can find many more excellent resources at [fsharp.org](https://fsharp.org).

This tutorial also assumes some knowledge of WPF and MVVM.

Suggestions for improvements are welcome. For large changes, please open an issue. For small changes (e.g. typos), simply submit a PR.

Table of contents
-----------------

* [The MVU (Elm/Elmish) architecture](#the-mvu-elmelmish-architecture)
  + [Model](#model)
  + [Message](#message)
  + [Update](#update)
  + [View in standard MVU (not Elmish.WPF)](#view-in-standard-mvu-not-elmishwpf)
  + [View in Elmish.WPF](#view-in-elmishwpf)
  + [Commands (and subscriptions)](#commands-and-subscriptions)
* [Some MVU tips for beginners](#some-mvu-tips-for-beginners)
  + [Normalize your model; use IDs instead of duplicating entities](#normalize-your-model-use-ids-instead-of-duplicating-entities)
  + [Use commands for anything impure](#use-commands-for-anything-impure)
  + [Child components and scaling](#child-components-and-scaling)
  + [Optimize easily with memoization](#optimize-easily-with-memoization)
* [Getting started with Elmish.WPF](#getting-started-with-elmishwpf)
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
    - [`subModelSeqSelectedItem`](#submodelseqselecteditem)
    - [`oneWaySeq`](#onewayseq)
  + [Lazy bindings](#lazy-bindings)
  + [Mapping bindings](#mapping-bindings)
    - [Example use of `mapModel` and `mapMsg`](#example-use-of-mapModel-and-mapMsg)
    - [Theory behind `mapModel` and `mapMsg`](#theory-behind-mapModel-and-mapMsg)
* [Additional resources](#additional-resources)

The MVU (Elm/Elmish) architecture
---------------------------------

MVU stands for Model-View-Update. It is a purely functional front-end architecture commonly used in Elm, a strongly typed pure functional language that compiles to JavaScript.

### Model

The “model” part of the MVU name refers to an immutable data structure that contains all the state in your app. By “all the state” we mean all the state that influences any kind of domain/business logic, and all the state that is needed to render the UI. By storing all the state in a single “atom”, data synchronization problems between different parts of the app are a thing of the past. Note that the model is concerned with domain concepts, not UI concepts. Ideally (if not always in practice), you should be able to use the same model to target different UIs using the MVU pattern (a WPF app, a React web app using [Feliz](https://github.com/Zaid-Ajaj/Feliz/), a console app using [Terminal.Gui.Elmish](https://github.com/DieselMeister/Terminal.Gui.Elmish), etc.)

For example, the type definition below may be the whole state for an app containing a single counter that you can increment/decrement by a customizable step size (the classic “hello world” of MVU apps):

```f#
type Model = {
  Count: int
  StepSize: int
}
```

The above model contains all the state that is needed to render the UI for such a simple counter app.

Additionally, in MVU, the model also comes with an `init` function that simply returns the app’s initial state:

```f#
let init () = {
  Count = 0
  StepSize = 1
}
```

### Message

While not part of the MVU name, the message is a central component. It’s just a type that specifies everything that can happen in your app – all the reasons your state may change (all of the “events” in the app, if you will). It’s typically modelled by a discriminated union.

For example, the type definition below may describe all the possible things that can happen in the counter app described above:

```f#
type Msg =
  | Increment
  | Decrement
  | SetStepSize of int
```

Messages are sent (known in the MVU world as “dispatched”) by the UI. We’ll get back to that.

As with the model, the message type is concerned with the domain, and is ideally unrelated to the UI platform.

### Update

The “update” part of the MVU name refers to the function that is responsible for updating your model in response to incoming messages. It has the signature `'msg -> 'model -> 'model`. In other words, it is a pure function that accepts a message (something that happened) and the old state, and returns the new state.

For example, for the counter app we have defined the model and message types for, the `update` function will look like this:

```f#
let update (msg: Msg) (model: Model) : Model =
  match msg with
  | Increment -> { model with Count = model.Count + model.StepSize}
  | Decrement -> { model with Count = model.Count - model.StepSize}
  | SetStepSize i -> { model with StepSize = i }  
```

### View in standard MVU (not Elmish.WPF)

This is where MVU frameworks will differ, since every UI technology is different.

At its core, `view` is a function that accepts 1) a model and 2) a function to dispatch messages, and returns something that specifies how the UI will be rendered. This may in theory be the actual UI, though that would be very inefficient. Generally, `view` returns a “shadow DOM” (a cheap object graph reflecting the UI) that the framework will intelligently compare with the actual UI so that only the changed parts of the UI will be updated.

In other words: **In MVU, the UI is simply a function of the current model**.

For example, the function below shows how the UI function might look like for the counter app above (using an imaginary UI library/syntax):

```f#
let view (model: Model) (dispatch: Msg -> unit) =
  Container(
    Children = [
      Paragraph(Text = sprintf "Current count: %i" model.Count)
      IntegerInput(
        Label = "Step size",
        Value = model.StepSize,
        OnChange = fun value -> dispatch (SetStepSize value)
      )
      Button(
        Text = "Decrement",
        OnClick = fun () -> dispatch Decrement
      )
      Button(
        Text = "Increment",
        OnClick = fun () -> dispatch Increment
      )
    ]
  )
```

Note that the core Elmish library defines the following type alias:

```f#
type Dispatch<'msg> = 'msg -> unit
```

Therefore, you will normally see `dispatch` typed as `Dispatch<'msg>` instead of `'msg -> unit`.

### View in Elmish.WPF

The `view` example above shows *dynamic views*, which is how “proper” MVU works. Creating views as a simple function of the model is a very powerful technique, is conceptually very simple, and allows for good composability.

In Elmish.WPF, however, the views are defined externally in XAML. The UI is *static* and is not defined or changed by the `view` code; hence, Elmish.WPF is said to use *static views*.

You set up bindings in the XAML views as you normally would if using MVVM. Then, in the `view` function, you use Elmish.WPF to declaratively create a “view model” of sorts that contain the data the view will bind to. Therefore the `view` function is normally called `bindings` in Elmish.WPF.

For example, the counter app may look like this:

```f#
let bindings () : Binding<Model, Msg> list = [
  "CounterValue" |> Binding.oneWay (fun m -> m.Count)
  "Increment" |> Binding.cmd Increment
  "Decrement" |> Binding.cmd Decrement
  "StepSize" |> Binding.twoWay(
    (fun m -> float m.StepSize),
    (int >> SetStepSize)
  )
]
```

The actual bindings will be explained in detail later, but explained simply, the code above will create a view-model with:

* an `int` get-only property `CounterValue` returning `model.Count`
* two get-only properties `Increment` and `Decrement` that are `ICommand`s that can always execute and, when executed, dispatches the `Increment` and `Decrement` messages, respectively
* a `float` get-set property `StepSize ` returning `model.StepSize` and which, when set, dispatches the `SetStepSize` message with the number

Another important difference between normal MVU `view` functions and Elmish.WPF’s `update`  function is that `view` is called every time the model has been updated, whereas `bindings` is only called once, when the “view model” is initialized. After that, it is the functions used in the bindings themselves that are called when the model is updated. Therefore, `bindings` do not accept a `model` or `dispatch` parameter. The `model` is instead passed separately in each binding, and the `dispatch` isn’t visible at all; you simply specify the message to be dispatched, and Elmish.WPF will take care of dispatching the message.

### Commands (and subscriptions)

This is yet another part of MVU that is not in the name. Not to be confused with WPF’s `ICommand`, the command in MVU is the only way you do side effects.

Think about it: If the update function must be pure, how can we do side effects like making an HTTP call or reading from disk? Or alternatively, if we decided to make `update` impure (which is possible in F#, but not in Elm) and do some long-running IO there, wouldn’t that block the whole app (since the update loop can only process one message at a time for concurrency reasons)?

The answer is that there are actually two variants of the `update` function: For very simple apps, as shown above, you can use the simple `update` version that just returns the new model. For more complex apps that need to use commands, the `update` function can return both the new model and a command in a tuple:

```f#
update: 'msg -> 'model -> 'model * Cmd<'msg>
```

What is a command, you ask? It’s simply the “top level” of three type aliases:

```f#
type Dispatch<'msg> = 'msg -> unit
type Sub<'msg> = Dispatch<'msg> -> unit
type Cmd<'msg> = Sub<'msg> list
```

We have encountered `Dispatch<'msg>` previously. It is the type of the `dispatch` argument to the normal MVU `view` function. It is simply an alias for a function that accepts a message and sends it to the MVU update loop so that it ends up in `update`.

The next alias, `Sub<'msg>` (short for “subscription”) is simply a function that accepts a dispatcher and returns `unit`. This function can then dispatch whatever messages it wants when it wants, e.g. by setting up event subscriptions.

For example, here is such a function that, when called, will start dispatching a `SetTime` message every second. The whole `timerTick` function (without applying `dispatch`) has the signature `Sub<Msg>`:

```f#
let timerTick (dispatch: Dispatch<Msg>) =
  let timer = new System.Timers.Timer(1000.)
  timer.Elapsed.Add (fun _ -> dispatch (SetTime DateTimeOffset.Now))
  timer.Start()
```

This is the kind of function that you pass to `Program.withSubscription`, which allows you to start arbitrary non-UI message dispatchers when the app starts. For example, you can start timers (as shown above), subscribe to other non-UI events, start a `MailboxProcessor`, etc.

The final alias, `Cmd<'msg>`, is just a list of `Sub<'msg>`, i.e. a list of `Dispatch<'msg> -> unit` functions. In other words, the `update` function can return a list of `Dispatch<'msg> -> unit` functions that the MVU update loop will execute. These functions, as you saw above, can dispatch any message at any time. Therefore, if you need to do impure stuff such as calling a web API, you simply create a function accepting `dispatch`, perform the call there (likely using `async`), and use the `dispatch` argument to dispatch a message when you receive a response.

In other words, you don’t call the impure functions yourself; the MVU library calls them for you. Furthermore, from the point of view of your model, everything happens asynchronously (in the sense that your app and update loop continues without waiting on a response, and reacts to the “response” message when it arrives).

For example:

* The user clicks a button to log in, which dispatches a `SignInRequested` message
* The `update` function returns a new model with an `IsBusy = true` value (which can be to show an animation such as a spinner) as well as a command that actually calls the API
* The MVU loop calls the command
* The app continues to work normally - the spinner spins because `IsBusy = true`, and any other messages are processed as they would normally be. Note that you are of course free to process messages differently based on the fact that `IsBusy = true`; for example, you may choose to ignore additional `SignInRequested` messages.
* When the API call returns, the function that called the API dispatches a suitable message based on the result (e.g. `SignInSuccessful` or `SignInFailed`)

Elmish has several helpers in the `Cmd` module to easily create commands from normal functions, but if they don’t suit your use-case, you can always write a command directly as a list of `Dispatch<'msg> -> unit` functions.

Some MVU tips for beginners
---------------------------

### Normalize your model; use IDs instead of duplicating entities

It is generally recommended that you aggressively normalize your model. This is because everything is (normally) immutable, so if a single entity occurs multiple places in your model and that entity should be updated, it must be updated every place it occurs. This opens up for state synchronization bugs.

For example, say you have an app that can display a list of books, and you can click on a book in the list to open a detail view of that book. You might think to represent it with the following model:

```f#
type Model = {
  Books: Book list
  DetailView: Book option
}
```

However, what if you now want to edit a book? The book may exist in two places – both in the list, and in the `DetailView` property.

A better solution is to have the list be the only place to store the `Book` objects, and then simply refer to books by ID everywhere else:

```f#
type Model = {
  Books: Book list
  DetailView: BookId option
}
```

(You don’t have to use `list`; often it will make sense to have `Map<BookId, Book>` to easily and efficiently get a book by its ID.)

This principle also extends to data in messages: If you have a choice between passing an entity ID and a complete entity object in a message, using an entity ID will usually be the better choice (even if it may not be immediately obvious).


### Use commands for anything impure

Keep the XAML (and any code-behind) focused on the view, keep `bindings` focused on bindings, and keep your model and `update`  pure. If you need to do anything impure, that's what `Command` is for, whether it's writing to disk, connecting to a DB, calling a web API, talking to actors, or anything else. All impure operations can be implemented using commands.

Note that there's nothing stopping you from having mutable state outside your model. For example, if you have persistent connections (e.g. SignalR) that you need to start and stop during the lifetime of your app, you can define them elsewhere and use  them in commands from your `update`. If you need an unknown number of them, such as one connection per item in a list in your model, you can store them in a dictionary or similar, keyed by the item's ID. This allows you to create, dispose, and remove items according to the data in your model.


### Child components and scaling

When starting out with MVU, it’s easy to fall into the trap of thinking ahead and wondering “how can I split my model/message/update/view into separate components?” For example, if you have two separate “pages” in your app, you might be inclined to think that each page should have its own separate model, message, update, and view. While this technique is needed with many other non-MVU architectures, it is often counterproductive in MVU.

Before delving into the problems, let’s see how it’s done:

```f#
module Child =

  type Model = { ... }
  type Msg = ...
  let update msg model = ...
  
module Parent =

	type Model = {
	  ...
	  Child: Child.Model
  }

	type Msg =
		...
		| ChildMsg of Child.Msg

	let update msg model =
		match msg with
		...
		| ChildMsg of childMsg -> { model with Child = Child.update childMsg model }
```

As you can see, there’s some boilerplate involved in the parent component: You must have a model field for the child model, a wrapping message case for the child message, and an `update` branch that passes the child message on to the child model.

Now for the problems.

One important problem is that often, “child components” are not in fact separate from their parents, but need access to some of the parent state. Continuing the book example above, say that you want to split the app into a “list component” and a “detail component” with separate models. If you want to have auto-complete of author names when editing a book in the detail component, you need access to the list all books. The only way to accomplish that reliably is to have the complete book list in the child component, too. But that means that every time you update a book, you need to remember to update it in the child component, too. This incurs boilerplate for every piece of duplicated state (since you must have a child message case for updating each piece of duplicated state), and, again, easily causes state synchronization bugs.

Another important problem, again following from the fact that the components are often not separate, is that a child component might need to communicate with its parent. For example, when saving a book, the parent component needs to get the updated book in a message, but the child component can only dispatch its child message type. There are ways to solve this (e.g. make the child `update` also return a separate “parent message” type, or have the parent intercept certain child messages), but all of them are usually unnecessary complications and not without drawbacks.

What should you do instead, then? The answer is, simply put, to scale the model/message/update/view **separately**, and **only when needed**. It is highly recommended that you read the following reddit thread replies by user `rtfeldman`:

* [Elm Architecture with a Redux-like store pattern](https://www.reddit.com/r/elm/comments/5xdl9z/elm_architecture_with_a_reduxlike_store_pattern/dehrcx8/)
* [How to structure Elm with multiple models?](https://www.reddit.com/r/elm/comments/5jd2xn/how_to_structure_elm_with_multiple_models/dbuu0m4/)

### Optimize easily with memoization

First: Never optimize prematurely. Only optimize if you can actually measure that a certain piece of code is giving you problems.

That said: Since everything in MVU is just (pure) functions, functions, and more functions, *memoization* is a technique that will allow you to easily skip work if inputs are equal. Memoization is simply about storing the inputs and outputs, and if the function is called with a known value, the already computed result is returned. If not, the result is computed by calling the actual function, and the result is stored to be reused later.

In general, there are several ways to memoize: You can memoize all inputs/outputs (may be memory heavy), or just the latest; you can memoize based on structural comparison of inputs (may be expensive), or use referential equality. In MVU architectures, you often need to ensure that when you use parts of your model to compute a result, you only compute the result once until the input changes. Specifically, you might not care about remembering old values, because generally these will never be used. In that case, you can often get very far with this general memoization implementation that memoizes only the last computed value and stores it using the input reference (which works because everything is normally immutable):

```f#
let memoize (f: 'a -> 'b) : 'a -> 'b =
  let mutable inputOutput = None
  fun x ->
    match inputOutput with
    | Some (x', res) when LanguagePrimitives.PhysicalEquality x x' -> res
    | _ ->
        let res = f x
        inputOutput <- Some (x, res)
        res
```

Usage:

```f#
let myExpensiveFun x = ...

let myExpensiveFunMemoized = memoize myExpensiveFun
```

Then you simply call `myExpensiveFunMemoized` instead of `myExpensiveFun` in the rest of your code.

It is important that `myExpensiveFunMemoized` is defined without arguments to ensure that `memoize` is applied only once. If you had written

```f#
let myExpensiveFunMemoized x = memoize myExpensiveFun x
```

then a new memoized version would be created each for each call, which defeats the purpose of memoizing it in the first place.

Furthermore, the implementation above only memoizes functions with a single input. If you need more parameters, you need to create `memoize2`, `memoize3`, etc. (You could also pass a single tuple argument, but that will never be referentially equal, so you’d need to use structural comparison instead. That might be prohibitively expensive if the input is, say, a large collection of domain objects. Alternatively you might use functionality similar to Elmish.WPF’s `elmEq` helper, which is explained later.)

Getting started with Elmish.WPF
-------------------------------

The [readme](https://github.com/elmish/Elmish.WPF/blob/master/README.md) has a “getting started” section that will have you up and running quickly with a simple skeleton solution.

The Elmish.WPF bindings
----------------------------

The Elmish.WPF bindings can be categorized into the following types:

* **One-way bindings**, for when you want to bind to a simple value.
* **Two-way bindings**, for when you want to bind to a simple value as well as update this value by dispatching a message. Used for inputs, checkboxes, sliders, etc. Can optionally support validation (e.g. provide an error message using `INotifyDataErrorInfo` that can be displayed when an input is not valid).
* **Command bindings**, for when you want a message to be dispatched when something happens (e.g. a button is clicked).
* **Sub-model bindings**, for when you want to bind to a complex object that has its own bindings. 
* **Sub-model window bindings**, for when you want to control the opening/closing/hiding of new windows.
* **Sub-model sequence bindings**, for when you want to bind to a collection of complex objects, each of which has its own bindings.
* **Other bindings** not fitting into the categories above
* **Lazy bindings**, optimizations of various other bindings that allow skipping potentially expensive computations if the input is unchanged

Additionally, there is a section explaining how most dispatching bindings allow you to wrap the dispatcher to support debouncing/throttling etc.

### One-way bindings

*Relevant sample: SingleCounter*

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

*Relevant sample: SingleCounter*

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

*Relevant sample: Validation*

You might want to display validation errors when the input is invalid. The best way to do this in WPF is through `INotifyDataErrorInfo`. Elmish.WPF supports this directly through the `twoWayValidate` bindings. In addition to `get` and `set`, this binding also accepts a third parameter that returns the error string to be displayed. This can be returned as `string option` (where `None` indicates no error), or `Result<_, string>` (where `Ok` indicates no error; this variant might allow you to easily reuse existing validation functions you have).

Keep in mind that by default, WPF controls do not display errors. To display errors, either use 3rd party controls/styles (such as [MaterialDesignInXamlToolkit](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit)) or add your own styles (the `Validation` sample in this repo demonstrates this).

There are also variants of the two-way validating bindings for option-wrapped values.

### Command bindings

*Relevant sample: SingleCounter*

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

*Relevant sample: SingleCounter*

A command may not always be executable. As you might know, WPF’s `ICommand` interface contains a `CanExecute` method that, if `false`, will cause WPF to disable the bound control (e.g. the button).

In the counter example, we might want to prohibit negative numbers, disabling the `Decrement` button when the `model.Count = 0`. This can be written using `cmdIf`:

```f#
"Decrement" |> Binding.cmdIf (
  (fun m -> Decrement),
  fun m -> m.Count >= 0
)
```

There are several ways to indicate that a command can‘t execute. The `cmdIf` binding has overloads for the following:

* `exec: 'model -> 'msg option`, where the command is disabled if `exec` returns `None` 
* `exec: 'model -> Result<'msg, _>`, where the command is disabled if `exec` returns `Error`
* `exec: 'model  -> 'msg * canExec: 'model -> bool` (as the example above shows), where the command is disabled if `canExec` returns `false` (and as with `cmd`, there is also an overload where `exec` is simply the message to dispatch)

#### Using the `CommandParameter`

*Relevant sample: UiBoundCmdParam*

There may be times you need to use the XAML `CommandParameter` property. You then need to use Elmish.WPF’s `cmdParam` binding, which works exactly like `cmd` but where `exec` function accepts the command parameter as its first parameter.

There is also `cmdParamIf` which combines `cmdParam` and `cmdIf`, allowing you to override the command’s `CanExecute`.

### Sub-model bindings

*Relevant sample: SubModel*

Sub-model bindings are used when you want to bind to a complex object that has its own bindings. In MVVM, this happens when one of your view-model properties is another view model with its own properties the UI can bind to.

Perhaps the most compelling use-case for sub-models is when binding the `ItemsSource` of a `ListView` or similar. Each item in the collection you bind to is a view-model with its own properties that is used when rendering each item. However, the same principles apply when there’s only a single sub-model. Collections are treated later; this section focuses on a single sub-model.

The `subModel` binding has three overloads, increasing in complexity depending on how much you need to customize the sub-bindings.

#### Level 1: No separate message type or customization of model for sub-bindings

This is sufficient for many purposes. The overload accepts two parameters:

* `getSubModel: 'model -> 'subModel` to obtain the sub-model 
* `bindings: unit -> Binding<'model * 'subModel, 'msg> list`, the bindings for the sub-model

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

* recursive models
* proper “child components” with their own model/update/bindings unrelated to their parent

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

*Relevant sample: SubModelOpt*

You can also use the `subModelOpt` binding. The signature is the same as the variants described above, except that `getSubModel` returns `'subModel option`. The UI will receive `null` when the sub-model is `None`.

Additionally, these bindings have an optional `sticky: bool` parameter. If `true`, Elmish.WPF will “remember” and return the most recent non-null sub-model when the `getSubModel` returns `None`. This can be useful for example when you want to animate away the UI for the sub-component when it’s set to `None`. If you do not use `sticky`, the UI will be cleared at the start of the animation, which may look weird.

### Sub-model window bindings

*Relevant sample: NewWindow*

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

*Relevant sample: SubModelSeq*

If you understand `subModel`, then `subModelSeq` isn’t much more complex. It has similar overloads, but instead of returning a single sub-model, you return `#seq<'subModel>`. Furthermore, all overloads have an additional parameter `getId` (which for the “level 1” and “level 2” overloads has signature `'subModel -> 'id`) that gets a unique identifier for each model. This identifier must be unique among all sub-models in the collection, and is used to know which items to add, remove, re-order, and update.

The `toMsg` parameter in the “level 2” and “level 3” overloads has the signature `'id * 'subMsg -> 'msg` (compared with just `'subMsg -> 'msg` for `subModel`). For this parameter you would typically use a parent message case that wraps both the child ID and the child message. You need the ID to know which sub-model it came from, and thus which sub-model to pass the message along to.

Finally, in the “level 3” overload that allows you to transform the model used for the bindings, the `getId` parameter has signature `'bindingModel -> 'id` (instead of `'subModel -> 'id` for the two simpler overloads).

### Other bindings

There are two special bindings not yet covered.

#### `subModelSeqSelectedItem`

*Relevant sample: SubModelSelectedItem*

The section on model normalization made it clear that it’s better to use IDs than complex objects in messages. This means that for bindings to the selected value of a `ListBox` or similar, you’ll likely have better luck using `SelectedValue` and `SelectedValuePath` rather than `SelectedItem`.

Unfortunately some selection-enabled WPF controls only have `SelectedItem` and do not support `SelectedValue` and `SelectedValuePath`. Using `SelectedItem` is particularly cumbersome in Elmish.WPF since the value is not your sub-model, but an instance of the Elmish.WPF view-model. To help with this, Elmish.WPF provides the `subModelSelectedItem` binding.

This binding works together with a `subModelSeq` binding in the same binding list, and allows you to use the `subModelSeq` binding’s IDs in your model while still using `SelectedItem` from XAML. For example, if you use `subModelSeq` to display a list of books identified by a `BookId`, the `subModelSelectedItem` binding allows you to use `SelectedBook: BookId` in your model.

The `subModelSelectedItem` binding has the following parameters:

* `subModelSeqBindingName: string`, where you identify the binding name for the corresponding `subModelSeq` binding
* `get: 'model -> 'id option`, where you return the ID of the sub-model in the `subModelSeq` binding that should be selected
* `set: 'id option -> 'msg`, where you return the message to dispatch when the selected item changes (typically this will be a message case wrapping the ID).

You bind the `SelectedItem` of a control to the `subModelSelectedItem` binding. Then, Elmish.WPF will take care of the following:

* When the UI retrieves the selected item, Elmish.WPF gets the ID using `get`, looks up the correct view-model in the `subModelSeq` binding identified by `subModelSeqBindingName`, and returns that view-model to the UI.
* When the UI sets the selected item (which it sets to an Elmish.WPF view-model), Elmish.WPF calls `set` with the ID of the sub-model corresponding to that view-model.

#### `oneWaySeq`

*Relevant sample: OneWaySeq*

In some cases, you might want to have a one-way binding not to a single, simple value, but to a potentially large collection of simple values. If you use `oneWay` for this, the entire list will be replaced and re-rendered each time the model updates.

In the special case that you want to bind to a collection of **simple** (can be bound to directly) and **distinct** values, you can use `oneWaySeq`. This will ensure that only changed items are replaced/moved.

The `oneWaySeq` binding has the following parameters:

* `get: 'model -> #seq<'a>`, to retrieve the collection
* `itemEquals: 'a -> 'a -> bool`, to determine whether an item has changed
* `getId: 'a -> 'id`, to track which items are added, removed, re-ordered, and changed

If the values are not simple (e.g. not strings or numbers), then you can instead use `subModelSeq` to set up separate bindings for each item. And if the values are not distinct (i.e., can not be uniquely identified in the collection), then Elmish.WPF won’t be able to track which items are moved, and you can’t use this optimization.

Note that you can always use `subModelSeq` instead of `oneWaySeq` (the opposite is not true.) The `oneWaySeq` binding is slightly simpler than `subModelSeq` if the elements are simple values that can be bound to directly.

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

Sometimes duplicate mapping code exists across several bindings. The duplicate mappings could be from the parent model to a common child model or it could be the wrapping of a child message in a parent message, which might depend on the parent model. The duplicate mapping code can be extracted and written once using the mapping functions `mapModel`, `mapMsg`, and `mapModelWithMsg`.

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

Additional resources
--------------------

* The [Elmish.WPF readme](https://github.com/elmish/Elmish.WPF/blob/master/README.md) contains
  * a “getting started” section that will get you quickly up and running
  * a FAQ with miscellaneous useful information
