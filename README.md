WPF done the Elmish Way
=======================

[![NuGet version](https://img.shields.io/nuget/v/Elmish.WPF.svg)](https://www.nuget.org/packages/Elmish.WPF) [![NuGet downloads](https://img.shields.io/nuget/dt/Elmish.WPF.svg)](https://www.nuget.org/packages/Elmish.WPF) [![Build status](https://img.shields.io/appveyor/ci/cmeeren/elmish-wpf/master.svg?label=master)](https://ci.appveyor.com/project/cmeeren/elmish-wpf/branch/master)

Never write a ViewModel class again!

This library uses [Elmish](https://elmish.github.io/elmish), an Elm architecture implemented in F#, to build WPF applications. Elmish was originally written for [Fable](http://fable.io) applications, however it was trimmed and packaged for .NET as well.

Recommended resources
---------------------

* The [Elmish docs site](https://elmish.github.io/elmish) explains the general Elm architecture and principles.
* The [Elmish.WPF samples](https://github.com/elmish/Elmish.WPF/tree/master/src/Samples) provide many concrete usage examples.
* The [official Elm guide](https://guide.elm-lang.org) may also provide some guidance, but note that not everything is relevant. A significant difference between “normal” Elm architecture and Elmish.WPF is that in Elmish.WPF, the views are statically defined using XAML, and the “view” function does not render views, but set up bindings.

Getting started with Elmish.WPF
-------------------------------

See the [SingleCounter](https://github.com/elmish/Elmish.WPF/tree/master/src/Samples) sample for a very simple app. The central points are:

1. Create an F# Console Application (you can create a Windows application, but the core Elmish logs are currently only written to the console).

2. Define the model that describes your app’s state:

   ```F#
   type Model =
     { Count: int
       StepSize: int }
   ```

3. Define the various messages that can change your model:

   ```F#
   type Msg =
     | Increment
     | Decrement
     | SetStepSize of int
   ```

4. Define an `update` function that takes a message and a model and returns an updated model:

   ```F#
   let update msg m =
     match msg with
     | Increment -> { m with Count = m.Count + m.StepSize }
     | Decrement -> { m with Count = m.Count - m.StepSize }
     | SetStepSize x -> { m with StepSize = x }
   ```

5. Define the “view” function using the `Bindings` module. This is the central public API of Elmish.WPF. Normally this function is called `view` and would take a model and a dispatch function (to dispatch new messages to the update loop) and return the UI (e.g. a HTML DOM to be rendered), but in Elmish.WPF this function simply sets up bindings that XAML-defined views can use. Therefore, let’s call it `bindings` instead of `view`. In order to be compatible with Elmish it needs to have the same signature, but in many (most?) cases the `model` and `dispatch ` parameters will be unused:

   ```F#
   open Elmish.WPF
   
   let bindings model dispatch =
     [
       "CounterValue" |> Binding.oneWay (fun m -> m.Count)
       "Increment" |> Binding.cmd (fun m -> Increment)
       "Decrement" |> Binding.cmd (fun m -> Decrement)
       "StepSize" |> Binding.twoWay
         (fun m -> float m.StepSize)
         (fun newVal m -> int newVal |> SetStepSize)
     ]
   ```

   The strings identify the binding names to be used in the XAML views. The [Binding module](https://github.com/elmish/Elmish.WPF/blob/master/src/Elmish.WPF/Binding.fs) has many functions to create various types of bindings.

6. Create a WPF user control library project to hold you XAML files, add a reference to this project from your Elmish project, and define your views and bindings in XAML:

   ```xaml
   <Window
       x:Class="MyNamespace.MainWindow"
       xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
       xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
     <StackPanel Orientation="Horizontal">
       <TextBlock Text="{Binding CounterValue}" />
       <Button Command="{Binding Decrement}" Content="-" />
       <Button Command="{Binding Increment}" Content="+" />
       <TextBlock Text="{Binding StepSize}" />
       <Slider Value="{Binding StepSize}" TickFrequency="1" Minimum="1" Maximum="10" />
     </StackPanel>
   </Window>
   ```

7. Add the entry point to your console project:

   ```F#
   open System
   open Elmish
   
   [<EntryPoint; STAThread>]
   let main argv =
     Program.mkSimple init update bindings
     |> Program.runWindow (MainWindow())
   ```

   `Program.runWindow` will instantiate an `Application ` and set the window’s `DataContext` to the bindings you defined.

8. Profit! :)

For more complicated examples and other `Binding` functions, see the [samples](https://github.com/elmish/Elmish.WPF/tree/master/src/Samples).

FAQ
---

#### Do I have to use the project structure outlined above?

Not at all.

#### Can I instantiate `Application` myself?

Yes, just do it before calling `Program.runWindow` and it will automatically be used. You might need this if you have application-wide resources in a `ResourceDictionary`, which might require you to instantiate the application before instantiating the main window you pass to `Program.runWindow`.

#### Can I open new windows/dialogs?

Probably not; there is no explicit support for this in Elmish.WPF, and there is no simple way for users to set the `DataContext` of new windows in Elmish.WPF without explicit support. In any case, making use of anything not available through bindings (such as opening and closing windows) would mean that your UI would no longer be a simple function of your model, which is a central point of the Elm architecture. This would be a complication of your architecture, since it would need to be controlled outside the normal update loop. We instead recommend using custom-made dialogs; see the [SubModelOpt](https://github.com/elmish/Elmish.WPF/tree/master/src/Samples) sample for a very simple example. This method also works great with libraries with ready-made MVVM-friendly dialogs, e.g. those in [Material Design In XAML Toolkit](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit). That being said, if you come up with a good way to solve this, contributions are welcome!