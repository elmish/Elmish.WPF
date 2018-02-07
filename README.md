WPF done the Elmish Way
=======

[![NuGet version](https://badge.fury.io/nu/Elmish.WPF.svg)](https://badge.fury.io/nu/Elmish.WPF)

Never write a ViewModel class again!

This library uses [fable-elmish](https://fable-elmish.github.io/), an Elm architecture implemented in F#, to build WPF applications. Fable-elmish was originally written for [Fable](https://github.com/fable-compiler) applications, however it was trimmed and packaged for .NET as well. It is highly recommended to have a look at the [elmish docs site](https://fable-elmish.github.io/elmish/) if you are not familiar with the Elm architecture.

Getting started with Elmish.WPF
------
* Create an F# Windows Application (or Console) project. This is where your Elmish model will live.
* Add nuget package `Elmish.WPF` to to your Elmish project.
* Create a WPF Class Library project. This is where your XAML views will live.
* Reference your View project in your Elmish project.

The Elmish Stuff
------
Here is an example of an Elmish model (`Model`) with a composite model inside of it (`ClockModel`) and the corresponding messages:
```ocaml
    type ClockMsg =
        | Tick of DateTime

    type ClockModel =
        { Time: DateTime }

    type Msg =
        | ClockMsg of ClockMsg
        | Increment
        | Decrement
        | SetStepSize of int

    type Model = 
        { Count: int
          StepSize: int
          Clock: ClockModel }
```
The init function returns your initial state, and each model gets an update function for message processing:
```ocaml
    let init() = { Count = 0; StepSize = 1; Clock = { Time = DateTime.Now }}
    
    let clockUpdate (model:ClockModel) = function
        | Tick t -> { model with Time = t }

    let update (model:Model) = function
        | Increment -> { model with Count = model.Count + model.StepSize }
        | Decrement -> { model with Count = model.Count - model.StepSize }
        | SetStepSize n -> { model with StepSize = n }
        | ClockMsg m -> { model with Clock = clockUpdate m model.Clock }
```
Subscriptions, which are events sent from outside the view or the dispatch loop, are created using `Cmd.ofSub`. For example, dispatching events on a timer:
```ocaml
    let timerTick dispatch =
        let timer = new System.Timers.Timer 1.
        timer.Elapsed.Subscribe (fun _ -> dispatch (System.DateTime.Now |> Tick |> ClockMsg)) |> ignore
        timer.Enabled <- true
        timer.Start()

    let subscribe model =
        Cmd.ofSub timerTick
```

Binding the Elmish to the XAML
------
Bindings in your XAML code will look like typical bindings, but a bit of extra code is needed to map those bindings to your Elmish model. These are the viewBindings, which expose parts of the model to the view. 

There are helper functions to create bindings located in the `Binding` module:
* `oneWay`
  * Basic source-to-view binding. Maps to `BindingMode.OneWay`.
  * Takes a getter (`'model -> 'a`)
* `twoWay`
  * Binding from source to view, or view to source, and usually used for input controls. Maps to `BindingMode.TwoWay` or `BindingMode.OneWayToSource`.
  * Takes a getter (`'model -> 'a`) and a setter (`'a -> 'model -> 'msg`) that returns a message.
* `twoWayValidation`
  * Binding from source to view, or view to source, and usually used for input controls. Maps to `BindingMode.TwoWay` or `BindingMode.OneWayToSource`. Setter will implement validation which is exposed to the view through typical `INotifyDataErrorInfo` properties.
  * Takes a getter (`'model -> 'a`) and a setter (`'a -> 'model -> Result<'msg,string>`) that indicates whether the input is valid or not.
* `cmd`
  * Basic command binding
  * Takes an execute function (`'model -> 'msg`)
* `cmdIf`
  * Conditional command binding
  * Takes an execute function (`'model -> 'msg`) and a canExecute function (`'model -> bool`)
* `vm`
  * Composite model binding
  * Takes a getter (`'model -> 'a`) and the composite model viewBindings, where `'a` is your composite model member. 
* `oneWayMap`
  * Basic source-to-view binding with a map function. This should be used for cases where it is desirable to have one type in your model and return a different type to the view. This will be more performant than mapping directly in the getter.
  * Takes a getter (`'model -> 'a`) and a mapper (`'a -> 'b`).

The last string argument to each binding is the name of the property as referenced in the XAML binding.
```ocaml
    let view _ _ = 
        let clockViewBinding : ViewBindings<ClockModel,ClockMsg> =
            [ "Time" |> Binding.oneWay (fun m -> m.Time) ]

        [ "Increment" |> Binding.cmd (fun m -> Increment)
          "Decrement" |> Binding.cmdIf (fun m -> Decrement) (fun m -> m.StepSize = 1)
          "Count" |> Binding.oneWay (fun m -> m.Count)
          "StepSize" |> Binding.twoWay (fun m -> (double m.StepSize)) (fun v m -> v |> int |> SetStepSize)
          "Clock" |> Binding.vm (fun m -> m.Clock) clockViewBinding ClockMsg ]
```
Note:  A viewBinding created with `Binding.vm` would be bound to the DataContext of a user control.

Tying it all together
-----
Pass an instance of your main window into `Program.runWindow`. The DataContext of this instance will be automatically set to your main model.
```ocaml
   [<EntryPoint;STAThread>]
    let main argv = 
        Program.mkSimple init update view
        |> Program.withSubscription subscribe
        |> Program.runWindow (Elmish.CounterViews.MainWindow())
```

Credits
-----
This library would not have been possible without the elmish engine, [fable-elmish](https://github.com/fable-elmish/elmish), written by [et1975](https://github.com/et1975). This project technically has no tie to [Fable](http://fable.io/), which is an F# to JavaScript transpiler that is definitely worth checking out.
