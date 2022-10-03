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

- an `int` get-only property `CounterValue` returning `model.Count`
- two get-only properties `Increment` and `Decrement` that are `ICommand`s that can always execute and, when executed, dispatches the `Increment` and `Decrement` messages, respectively
- a `float` get-set property `StepSize` returning `model.StepSize` and which, when set, dispatches the `SetStepSize` message with the number

Another important difference between normal MVU `view` functions and Elmish.WPF’s `update`  function is that `view` is called every time the model has been updated, whereas `bindings` is only called once, when the “view model” is initialized. After that, it is the functions used in the bindings themselves that are called when the model is updated. Therefore, `bindings` do not accept a `model` or `dispatch` parameter. The `model` is instead passed separately in each binding, and the `dispatch` isn’t visible at all; you simply specify the message to be dispatched, and Elmish.WPF will take care of dispatching the message.

### Commands (and subscriptions)

This is yet another part of MVU that is not in the name. Not to be confused with WPF’s `ICommand`, the command in MVU is the only way you do side effects.

Think about it: If the update function must be pure, how can we do side effects like making an HTTP call or reading from disk? Or alternatively, if we decided to make `update` impure (which is possible in F#, but not in Elm) and do some long-running IO there, wouldn’t that block the whole app (since the update loop can only process one message at a time for concurrency reasons)?

The answer is that there are actually two variants of the `update` function: For very simple apps, as shown above, you can use the simple `update` version that just returns the new model. For more complex apps that need to use commands, the `update` function can return both the new model and a command in a tuple:

```f#
update: 'msg -> 'model -> 'model * Cmd<'msg>
```

What is a `Cmd<'msg>`, you ask? It’s simply the “top level” of three type aliases:

```f#
type Dispatch<'msg> = 'msg -> unit
type Sub<'msg> = Dispatch<'msg> -> unit
type Cmd<'msg> = Sub<'msg> list
```

We have encountered `Dispatch<'msg>` previously. It is the type of the `dispatch` argument to the normal MVU `view` function. It is simply an alias for a function that accepts a message and sends it to the MVU framework so that it ends up being passed into `update`.

The next alias, `Sub<'msg>` (short for “subscription”) is simply a function that accepts a dispatcher and returns `unit`. This function can then dispatch whatever messages it wants whenever it wants, e.g. by setting up event subscriptions.

For example, here is such a function that, when called, will start dispatching a `SetTime` message every second. The whole `timerTick` function (without applying `dispatch`) has the signature `Sub<Msg>`:

```f#
let timerTick (dispatch: Dispatch<Msg>) =
  let timer = new System.Timers.Timer(1000.)
  timer.Elapsed.Add (fun _ -> dispatch (SetTime DateTimeOffset.Now))
  timer.Start()
```

This is the kind of function that you pass to `Program.withSubscription`, which allows you to start arbitrary non-UI message dispatchers when the app starts. For example, you can start timers (as shown above), subscribe to other non-UI events, start a `MailboxProcessor`, etc.

The final alias, `Cmd<'msg>`, is just a list of `Sub<'msg>`, i.e. a list of `Dispatch<'msg> -> unit` functions. In other words, the `update` function can return a list of `Dispatch<'msg> -> unit` functions that the MVU framework will execute by providing a dispatch function. These functions, as you saw above, can then dispatch any message at any time. Therefore, if you need to do impure stuff such as calling a web API, you simply create a function accepting `dispatch`, perform the work within it, and then use the `dispatch` argument (provided by the MVU framework) to dispatch further messages (e.g. representing the result of the action) into the MVU event loop.

In other words, the `Cmd<'msg>` returned by `update` will be invoked by the MVU framework. From the point of view of your model, everything happens asynchronously: the MVU update loop executes the command and continues without waiting for it to complete, and the command may dispatch future messages into the event loop at any time.

For example:

- The user clicks a button to log in, which dispatches a `SignInRequested` message
- The `update` function returns a new model with an `IsBusy = true` value (which can be used to show an animation such as a spinner) as well as a command that asynchronously calls the API and, when the API responds, dispatches a message representing the response (e.g. `SignInSuccessful` or `SignInFailed`).
- The MVU framework updates the view using the new model and invokes the command by executing each function in the list with a `dispatch` function.
- The app continues to work as normal - the spinner spins because `IsBusy = true` and any other messages are processed as normal. Note that you are of course free to process messages differently based on the fact that `IsBusy = true`. For example, you may choose to ignore additional `SignInRequested` messages.
- When the API call finally returns, and the function that called the API uses its `dispatch` argument to dispatch a suitable message (e.g. `SignInSuccessful` or `SignInFailed`).

Elmish has several helpers in the `Cmd` module to easily create commands from normal functions, but if they don’t suit your use-case, you can always write a command directly as a list of `Dispatch<'msg> -> unit` functions.

Some MVU tips for beginners
---------------------------

### Normalize your model; use IDs instead of duplicating entities

It is generally recommended that you aggressively normalize your model. This is because everything is (normally) immutable, so if a single entity occurs multiple places in your model and that entity should be updated, it must be updated every place it occurs. This increases the chance of introducing state synchronization bugs.

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

Note that there's nothing stopping you from having mutable state outside your model. For example, if you have persistent connections (e.g. SignalR) that you need to start and stop during the lifetime of your app, you can define them elsewhere and use them in commands from your `update`. If you need an unknown number of them, such as one connection per item in a list in your model, you can store them in a dictionary or similar, keyed by the item's ID. This allows you to create, dispose, and remove items according to the data in your model.


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

- [Elm Architecture with a Redux-like store pattern](https://www.reddit.com/r/elm/comments/5xdl9z/elm_architecture_with_a_reduxlike_store_pattern/dehrcx8/)
- [How to structure Elm with multiple models?](https://www.reddit.com/r/elm/comments/5jd2xn/how_to_structure_elm_with_multiple_models/dbuu0m4/)

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

Additional resources
--------------------

The [Elmish.WPF readme](https://github.com/elmish/Elmish.WPF/blob/master/README.md) contains
  - a “getting started” section that will get you quickly up and running
  - a FAQ with miscellaneous useful information
