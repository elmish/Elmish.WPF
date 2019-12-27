WPF done the Elmish Way
=======================

[![NuGet version](https://img.shields.io/nuget/v/Elmish.WPF.svg)](https://www.nuget.org/packages/Elmish.WPF) [![NuGet downloads](https://img.shields.io/nuget/dt/Elmish.WPF.svg)](https://www.nuget.org/packages/Elmish.WPF) [![Build status](https://img.shields.io/appveyor/ci/cmeeren/elmish-wpf/master.svg?label=master)](https://ci.appveyor.com/project/cmeeren/elmish-wpf/branch/master)

**The good parts of MVVM (the data bindings) with the simplicity and robustness of an MVU architecture for the rest of your app. Never write a ViewModel class again!**

### Elevator pitch

Elmish.WPF is a **production-ready** library that allows you to write WPF apps with the robust, simple, well-known, and battle-tested MVU architecture, while still allowing you to use all your XAML knowledge and tooling to create UIs.

Some benefits of MVU you’ll get with Elmish.WPF is:

* Simple-to-understand, unidirectional data flow
* Single source of truth for all the state in your app
* Simple async/IO
* Immutable data
* Pure functions
* Great testability
* Simple optimization
* 78% more rockets 🚀

Even with static views, your central model/update code can follow an idiomatic Elmish/MVU architecture. You could, if you wanted, use the same model/update code to implement an app using a dynamic UI library such as [Fabulous](https://github.com/fsprojects/Fabulous) or [Fable.React](https://github.com/fable-compiler/fable-react), by just rewriting the “U” part of MVU.

**Static XAML views is a feature, not a limitation. See the FAQ for several unique benefits to this approach!**

Elmish.WPF uses [Elmish](https://github.com/elmish/elmish), an F# implementation of the MVU message loop.

### Sponsor

[![JetBrains logo](jetbrains.svg)](https://www.jetbrains.com/?from=Elmish.WPF)

Thanks to JetBrains for sponsoring Elmish.WPF with OSS licenses!

Recommended resources
---------------------

* The [Elmish.WPF tutorial](https://github.com/elmish/Elmish.WPF/blob/master/TUTORIAL.md) explains how to use Elmish.WPF, starting with general Elmish/MVU concepts and ending with complex bindings and optimizations.
* The [Elmish docs site](https://elmish.github.io/elmish) also explains the general MVU architecture and principles.
* The [Elmish.WPF samples](https://github.com/elmish/Elmish.WPF/tree/master/src/Samples) provide many concrete usage examples.
* Elm resources may also provide some guidance, but note that not everything is relevant. A significant difference between “normal” Elm architecture and Elmish.WPF is that in Elmish.WPF, the views are statically defined using XAML, and the “view” function does not render views, but set up bindings. See the [tutorial](https://github.com/elmish/Elmish.WPF/blob/master/TUTORIAL.md) for details.
  * [Official Elm guide](https://guide.elm-lang.org)
  * Two talks: [Summarising Elm scaling strategy](https://dev.to/elmupdate/summarising-elm-scaling-strategy-1bjn)
  * Reddit: [Resources regarding scaling Elm apps](https://www.reddit.com/r/elm/comments/65s0g4/resources_regarding_scaling_elm_apps/)
  * Reddit: [How to structure Elm with multiple models](https://www.reddit.com/r/elm/comments/5jd2xn/how_to_structure_elm_with_multiple_models/dbuu0m4/)
  * Reddit: [Elm Architecture with a Redux-like store pattern](https://www.reddit.com/r/elm/comments/5xdl9z/elm_architecture_with_a_reduxlike_store_pattern/)

Getting started with Elmish.WPF
-------------------------------

See the [SingleCounter](https://github.com/elmish/Elmish.WPF/tree/master/src/Samples) sample for a very simple app. The central points are:

1. Create an F# Console Application. (You can create a Windows application, but the core Elmish logs are currently only written to the console.)

   If targeting .NET Core 3, the project file should look like this:

   ```fsproj
   <Project Sdk="Microsoft.NET.Sdk.WindowsDesktop">
     
     <PropertyGroup>
       <OutputType>Exe</OutputType>  <!-- or WinExe -->
       <TargetFramework>netcoreapp3.0</TargetFramework>
       <UseWpf>true</UseWpf>
     </PropertyGroup>
     
     <!-- other stuff -->
   ```

   If targeting .NET Framework (4.6.1 or later), the project file should look like this:

   ```fsproj
   <Project Sdk="Microsoft.NET.Sdk">
     
     <PropertyGroup>
       <OutputType>Exe</OutputType>  <!-- or WinExe -->
       <TargetFramework>net471</TargetFramework>
     </PropertyGroup>
     
     <ItemGroup>
       <Reference Include="PresentationCore" />
       <Reference Include="PresentationFramework" />
       <Reference Include="WindowsBase" />
     </ItemGroup>
     
     <!-- other stuff -->
   ```

2. Add NuGet reference to package `Elmish.WPF`.

3. Define the model that describes your app’s state and a function that initializes it:

   ```F#
   type Model =
     { Count: int
       StepSize: int }
   
   let init () =
     { Count = 0
       StepSize = 1 }
   ```

4. Define the various messages that can change your model:

   ```F#
   type Msg =
     | Increment
     | Decrement
     | SetStepSize of int
   ```

5. Define an `update` function that takes a message and a model and returns an updated model:

   ```F#
   let update msg m =
     match msg with
     | Increment -> { m with Count = m.Count + m.StepSize }
     | Decrement -> { m with Count = m.Count - m.StepSize }
     | SetStepSize x -> { m with StepSize = x }
   ```

6. Define the “view” function using the `Bindings` module. This is the central public API of Elmish.WPF. Normally in Elm/Elmish this function is called `view` and would take a model and a dispatch function (to dispatch new messages to the update loop) and return the UI (e.g. a HTML DOM to be rendered), but in Elmish.WPF this function is in general only run once and simply sets up bindings that XAML-defined views can use. Therefore, let’s call it `bindings` instead of `view`.

   ```F#
   open Elmish.WPF
   
   let bindings () =
     [
       "CounterValue" |> Binding.oneWay (fun m -> m.Count)
       "Increment" |> Binding.cmd (fun m -> Increment)
       "Decrement" |> Binding.cmd (fun m -> Decrement)
       "StepSize" |> Binding.twoWay(
         (fun m -> float m.StepSize),
         (fun newVal m -> int newVal |> SetStepSize))
     ]
   ```

   The strings identify the binding names to be used in the XAML views. The [Binding module](https://github.com/elmish/Elmish.WPF/blob/master/src/Elmish.WPF/Binding.fs) has many functions to create various types of bindings.

7. Create a WPF user control library project to hold you XAML files, add a reference to this project from your Elmish project, and define your views and bindings in XAML:

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

8. Add the entry point to your console project:

   ```F#
   open System
   open Elmish.WPF
   
   [<EntryPoint; STAThread>]
   let main argv =
     Program.mkSimpleWpf init update bindings
     |> Program.runWindow (MainWindow())
   ```

   `Program.runWindow` will instantiate an `Application` and set the window’s `DataContext` to the bindings you defined.

9. Profit! :)

Further resources:

* The [Elmish.WPF tutorial](https://github.com/elmish/Elmish.WPF/blob/master/TUTORIAL.md) provides information on general MVU/Elmish concepts and how they apply to Elmish.WPF, as well as the various Elmish.WPF bindings.
* The [samples](https://github.com/elmish/Elmish.WPF/tree/master/src/Samples) are complete, working mini-apps demonstrating selected aspects of Elmish.WPF.

FAQ
---

#### Static views? Isn’t that just a half-baked solution that only exists due to a lack of better alternatives?

Not at all! 🙂

It’s true that static views aren’t as composable as dynamic views. It’s also true that at the time of writing, there are no solid, production-ready dynamic UI libraries for WPF (though there are no lack of half-finished attempts or proof-of-concepts: [Elmish.WPF.Dynamic](https://github.com/cmeeren/Elmish.WPF.Dynamic), [Fabulous.WPF](https://github.com/TimLariviere/Fabulous.WPF), [Skylight](https://github.com/gerardtoconnor/Skylight), [Uil](https://github.com/elmish/Uil)). Heck, it’s even true that Elmish.WPF was originally created with static views due to the difficulty of creating a dynamic UI library, as described in [issue #1](https://github.com/elmish/Elmish.WPF/issues/1).

However, Elmish.WPF’s static-view-based solution has several unique benefits:

- You can use your existing XAML and MVVM knowledge (that is, the best part of MVVM – the UI bindings – without having to deal with `NavigationService`s, `ViewModelLocator`s, state synchronization, `INotifyPropertyChanged`, etc.)
- Huge mindshare – there are tons of relevant XAML and MVVM resources on the net which can help with the UI and data binding part if you get stuck
- Automatic support for all 3rd party WPF UI libraries like [MaterialDesignInXamlToolkit](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit), since it just uses XAML and bindings (support for 3rd party libraries is commonly a major pain point for dynamic UI solutions)
- You can use the XAML designer (including design-time data binding)
- Automatically puts all the power of WPF at your fingertips, whereas dynamic UI solutions have [inherent limitations](https://github.com/cmeeren/Elmish.WPF.Dynamic/tree/e9f04b6e330754f045df093368fa4917c892399d#current-limitations) that are not easy to work around

In short, for WPF apps, a solution based on static XAML views is currently the way to go.

#### Do I have to use the project structure outlined above?

Not at all. The above example, as well as the samples, keep everything in a single project for simplicity (the samples have the XAML definitions in separate projects for technical reasons). For more complex apps, you might want to consider a more clear separation of UI and core logic. An example would be the following structure:

* A core library containing the model definitions and `update` functions.
  * This library can include a reference to Elmish (e.g. for the `Cmd` module helpers), but not to Elmish.WPF, which depends on certain WPF UI assemblies and has a UI-centred API (specifying bindings). This will ensure your core logic (such as the `update` function) is free from any UI concerns, and allow you to re-use the core library should you want to port your app to another Elmish-based solution (e.g. Fable.React).
* An entry point project that contains the `bindings` (or `view`) function and the call to `Program.runWindow`.
  * This project would reference the core library and `Elmish.WPF`.
* A view project containing the XAML-related stuff (windows, user controls, behaviors, etc.).
  * This could also be part of the entry point project, but if you’re using the new project format (like the samples in this repo), this might not work properly until .NET Core 3.0.

#### How can I test commands? What is the CmdMsg pattern?

Since the commands (`Cmd<Msg>`) returned by `init` and `update` are lists of functions, they are not particularly testable. A general pattern to get around this is to replace the commands with pure data that are transformed to the actual commands elsewhere:

* Create a `CmdMsg` union type with cases for each command you want to execute in the app.
* Make `init` and `update` return `model * CmdMsg list` instead of `model * Cmd<Msg>`. Since `init` and `update` now return data, they are much easier to test.
* Create a trivial/too-boring-to-test `cmdMsgToCmd` function that transforms a `CmdMsg` to the corresponding `Cmd`.
* Finally, create “normal” versions of `init` and `update` that you can use when creating `Program`. Elmish.WPF provides `Program.mkProgramWpfWithCmdMsg` that does this for you (but there’s no magic going on – it’s really easy to do yourself).

The [FileDialogs.CmdMsg sample](https://github.com/elmish/Elmish.WPF/tree/master/src/Samples) demonstrates this approach. For more information, see the [Fabulous documentation](https://fsprojects.github.io/Fabulous/Fabulous.XamarinForms/update.html#replacing-commands-with-command-messages-for-better-testability). For reference, here is [the discussion that led to this pattern](https://github.com/fsprojects/Fabulous/pull/320#issuecomment-491522737).

#### Can I instantiate `Application` myself?

Yes, just do it before calling `Program.runWindow` and it will automatically be used. You might need this if you have application-wide resources in a `ResourceDictionary`, which might require you to instantiate the application before instantiating the main window you pass to `Program.runWindow`.

#### Can I use design-time view models?

Yes. You need to structure your code so you have some place in your code that satisfies the following requirements:

* Must be able to instantiate a model and the associated bindings
* Must be reachable by the XAML views

There, use `ViewModel.designInstance` to create a view model instance that your XAML can use at design-time:

```F#
module MyAssembly.DesignViewModels
let myVm = ViewModel.designInstance myModel myBindings
```

Then use the following attributes wherever you need a design-time VM:

```XAML
<Window
    ...
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:vm="clr-namespace:MyAssembly;assembly=MyAssembly"
    mc:Ignorable="d"
    d:DataContext="{x:Static vm:DesignViewModels.myVm}">
```

When targeting .NET Framework, “Project code” must be enabled in the XAML designer for this to work.

When targeting .NET Core 3, the designer is buggy when it comes to design-time view models as used above. If you don’t get the data you expect, try to remove and re-add (e.g. undo) the `d:DataContext` line. That should make it work. You will have to do this as often as needed.

#### Can I open new windows/dialogs?

Sure! Just use `Binding.subModelWin`. It works like `Binding.subModel`, but has a `WindowState` wrapper around the returned model to control whether the window is closed, hidden, or visible. You can use both modal and non-modal windows/dialogs, and everything is a part of the Elmish core loop. Check out the [NewWindow sample](https://github.com/elmish/Elmish.WPF/tree/master/src/Samples).

#### How can I use Save File / Open File dialogs?

There’s a few things to remember regarding opening on the UI thread and not blocking the Elmish dispatch loop. Check out the [FileDialogs sample](https://github.com/elmish/Elmish.WPF/tree/master/src/Samples). In short, write a function like below, and call it using `Cmd.OfAsync`.

```f#
let save text =
  Application.Current.Dispatcher.Invoke(fun () ->
    let guiCtx = SynchronizationContext.Current
    async {
      do! Async.SwitchToContext guiCtx
      let dlg = Microsoft.Win32.SaveFileDialog ()
      // configure dialog (extensions etc.), show it, handle result
    }
  )
```

#### Can I bind to events and use behaviors?

Sure! Check out the [EventBindingsAndBehaviors sample](https://github.com/elmish/Elmish.WPF/tree/master/src/Samples). Note that you have to install the NuGet package `Microsoft.Xaml.Behaviors.Wpf`.
