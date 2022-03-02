module Elmish.WPF.ViewModel

open Microsoft.Extensions.Logging.Abstractions

/// Creates a design-time view model using the given model and bindings.
let designInstance (model: 'model) (bindings: Binding<'model, 'msg> list) =
  DynamicViewModel(model, ignore, bindings, 1, "main", NullLogger.Instance, NullLogger.Instance) |> box
