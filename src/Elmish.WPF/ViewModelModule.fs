module Elmish.WPF.ViewModel

open Microsoft.Extensions.Logging.Abstractions

/// Creates a design-time view model using the given model and bindings.
let designInstance (model: 'model) (bindings: Binding<'model, 'msg> list) =
  let args =
    { initialModel = model
      dispatch = ignore
      loggingArgs =
        { performanceLogThresholdMs = 1
          nameChain = "main"
          log = NullLogger.Instance
          logPerformance = NullLogger.Instance } }

  ViewModel(args, bindings) |> box