module Elmish.WPF.ViewModel

/// Creates a design-time view model using the given model and bindings.
let designInstance (model: 'model) (bindings: Binding<'model, 'msg, obj> list) =
  let args = ViewModelArgs.simple model

  ViewModel(args, bindings) |> box