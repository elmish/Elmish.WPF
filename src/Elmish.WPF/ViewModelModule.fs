module Elmish.WPF.ViewModel

/// <summary>
/// Creates a design-time view model using the given model and bindings.
/// This is useful for XAML design-time data support in WPF designers.
/// </summary>
/// <param name="model">The model instance to use at design time</param>
/// <param name="bindings">The list of bindings to configure</param>
/// <returns>A boxed DynamicViewModel suitable for design-time use</returns>
let designInstance (model: 'model) (bindings: Binding<'model, 'msg> list) =
    let args = ViewModelArgs.simple model

    DynamicViewModel(args, bindings) |> box