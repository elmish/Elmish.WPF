# Elmish.WPF Samples

This directory contains sample applications demonstrating different approaches to using Elmish.WPF.

## Two Binding Approaches

Elmish.WPF supports two different approaches for creating bindings between your F# model and WPF views:

### 1. Dynamic Bindings (Traditional Approach)

Located in the `Dynamic/` folder, these samples use the traditional dynamic binding approach with a `bindings()` function that returns a list of `Binding<Model, Msg>`.

**Characteristics:**
- More flexible and dynamic
- Bindings are defined at runtime
- Less boilerplate for simple scenarios
- All bindings defined in a single function

**Example:**
```fsharp
let bindings () : Binding<Model, Msg> list = [
    "CounterValue" |> Binding.oneWay (fun m -> m.Count)
    "Increment" |> Binding.cmd Increment
    "StepSize" |> Binding.twoWay ((fun m -> float m.StepSize), int >> SetStepSize)
]
```

**When to use:**
- Rapid prototyping
- Simple applications
- When you prefer functional composition over OOP
- When bindings need to be highly dynamic

### 2. Typed ViewModels (Static Approach)

Located in the `Typed/` folder, these samples use strongly-typed view models that inherit from `ViewModelBase<Model, Msg>`.

**Characteristics:**
- Compile-time type safety for WPF bindings
- Better IntelliSense support in XAML
- More familiar to WPF developers
- Each property is explicitly defined
- Better design-time support in Visual Studio/Blend

**Example:**
```fsharp
type CounterViewModel(args) =
    inherit ViewModelBase<Model, Msg>(args)
    
    member _.CounterValue =
        base.Get () (Binding.OneWayT.id >> Binding.mapModel (fun m -> m.Count))
    
    member _.StepSize
        with get () = base.Get () stepSizeBinding
        and set (v) = base.Set (v) stepSizeBinding
```

**When to use:**
- Large applications requiring strong typing
- When working with designers who need design-time data
- When you want compile-time validation of binding names
- Complex view models with many properties
- When integrating with existing WPF applications

## Sample Organization

### Dynamic Samples
- **SingleCounter** - Basic counter with increment/decrement
- **SubModel** - Nested models and view models
- **SubModelSeq** - Collections of sub-models
- **Validation** - Input validation examples
- **FileDialogs** - File dialog integration
- **Threading** - Async and threading examples
- ...and more

### Typed Samples
- **SubModelStatic** - Complex nested view models with static typing
- **SingleCounterTyped** - Typed version of the basic counter example

## Getting Started

1. Choose the approach that best fits your needs
2. Navigate to the corresponding folder (Dynamic or Typed)
3. Open the sample that most closely matches your use case
4. Study both the F# code (.fs files) and XAML files to understand the binding approach

## Migration Between Approaches

You can convert between approaches:
- **Dynamic to Typed**: Create a class inheriting from `ViewModelBase` and define properties for each binding
- **Typed to Dynamic**: Extract bindings into a `bindings()` function returning a list

Both approaches can coexist in the same application if needed.