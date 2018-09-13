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

Not at all. The above example, as well as the samples, keep everything in a single project for simplicity (the samples have the XAML definitions in separate projects for technical reasons). For more complex apps, you might want to consider a more clear separation of UI and core logic. An example would be the following structure:

* A core library containing the model definitions and `update` functions. This library can include a reference to Elmish (e.g. for the `Cmd` module helpers), but not to Elmish.WPF, which depends on certain WPF UI assemblies and has a UI-centred API (the `Binding` module). This will ensure your core logic (such as the `update` function) is free from any UI concerns, and allow you to re-use the core library should you want to port your app to another Elmish-based solution (e.g. using Fable).
* An entry point project that contains the `bindings` (or `view`) function and the call to `Program.runWindow`. This project would reference the core library and `Elmish.WPF`.
* A view project containing the XAML-related stuff (windows, user controls, behaviors, etc.). This could also be part of the entry point project, but if you’re using the new project format (like the samples in this repo), this might not work properly until .NET Standard 3.0.

#### Can I instantiate `Application` myself?

Yes, just do it before calling `Program.runWindow` and it will automatically be used. You might need this if you have application-wide resources in a `ResourceDictionary`, which might require you to instantiate the application before instantiating the main window you pass to `Program.runWindow`.

#### Can I open new windows/dialogs?

The short version: Yes, but depending on the use-case, this may not play well with the Elmish architecture, and it is likely conceptually and architecturally clearer to stick with some kind of dialog in the main window, using bindings to control its visibility.

The long version:

You can easily open modeless windows (using `window.Show()`) in  command and set the binding context of the new window to the binding context of the main window. The [NewWindow sample](https://github.com/elmish/Elmish.WPF/tree/master/src/Samples) demonstrates this. It is then, from Elmish’s point of view, absolutely no difference between the windows; the bindings and message dispatches work exactly the same as if you had used multiple user controls in a single window, and you may close the new window without Elmish being affected by it.

Note that the NewWindow sample (like the other samples) keep a very simple project structure where the views are directly accessible in the core logic, which allows for direct instantiation of new windows in the `update` function (or the commands it returns). If you want a clearer separation between UI and core logic as previously described, you would need to write some kind of navigation service abstraction and use inversion of control (such as dependency injection) to allow the core project to instantiate the new window indirectly using the navigation service without needing to reference the UI layer directly. Such architectural patterns of course go very much against the grain of Elmish and functional architecture in general.

While modeless windows are possible, if not necessarily pleasant or idiomatic, you can not use the same method to open modal windows (using `window.ShowDialog()`). This will block the Elmish update loop, and all messages will be queued and only processed when the modal window is closed.

Windows that semantically produce a result, even if you implement them as modeless, can be more difficult. An general example might be a window containing a data entry form used to create a business entity. In these cases, a “Submit” button may need to both dispatch a message containing the window’s result (done via `Binding.cmd` or similar), as well as close the window. This can be problematic, or at least cumbersome, when there is logic determining what actually happens when the “Submit” button is clicked (send the result, display validation errors, etc.). For more on this, see the discussion in [#24](https://github.com/elmish/Elmish.WPF/issues/24).

The recommended approach is to stick to what is available via bindings in a single window. In the case of new windows, this means instead using in-window dialogs, similar to how most SPAs (single-page applications) created with Elm or Elmish would behave. This allows the UI to be a simple function of your model, which is a central point of the Elm architecture (whereas opening and closing windows are events that do not easily derive from any model state). The [SubModelOpt sample](https://github.com/elmish/Elmish.WPF/tree/master/src/Samples) provides a very simple example of custom dialogs, and this method also works great with libraries with ready-made MVVM-friendly dialogs, e.g. those in [Material Design In XAML Toolkit](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit).
