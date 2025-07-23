namespace Elmish.WPF

open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Abstractions


type internal LoggingViewModelArgs =
    { performanceLogThresholdMs: int
      log: ILogger
      logPerformance: ILogger
      nameChain: string }

module internal LoggingViewModelArgs =

    let getNameChainFor nameChain name = sprintf "%s.%s" nameChain name

    let getNameChainForItem nameChain collectionBindingName itemId =
        sprintf "%s.%s.%s" nameChain collectionBindingName itemId

    let map nameChain v = { v with nameChain = nameChain }

    let none =
        { performanceLogThresholdMs = 1
          log = NullLogger.Instance
          logPerformance = NullLogger.Instance
          nameChain = "" }


/// <summary>
/// Arguments required to create a view model, including the initial model state,
/// dispatch function for messages, and logging configuration
/// </summary>
/// <typeparam name="'model">The type of the model</typeparam>
/// <typeparam name="'msg">The type of messages</typeparam>
type ViewModelArgs<'model, 'msg> =
    internal
        { initialModel: 'model
          dispatch: 'msg -> unit
          loggingArgs: LoggingViewModelArgs }

module ViewModelArgs =
    let internal create initialModel dispatch nameChain loggingArgs =
        { initialModel = initialModel
          dispatch = dispatch
          loggingArgs = LoggingViewModelArgs.map nameChain loggingArgs }

    /// <summary>
    /// Maps both the model and message types of ViewModelArgs
    /// </summary>
    /// <param name="mapModel">Function to transform the model type</param>
    /// <param name="mapMsg">Function to transform the message type</param>
    /// <param name="v">The ViewModelArgs to map</param>
    /// <returns>New ViewModelArgs with transformed types</returns>
    let map mapModel mapMsg v =
        { initialModel = v.initialModel |> mapModel
          dispatch = mapMsg >> v.dispatch
          loggingArgs = v.loggingArgs }

    /// <summary>
    /// Creates ViewModelArgs without logging enabled
    /// </summary>
    /// <param name="initialModel">The initial model state</param>
    /// <param name="dispatch">The message dispatch function</param>
    /// <returns>ViewModelArgs configured without logging</returns>
    let createWithoutLogging initialModel dispatch =
        { initialModel = initialModel
          dispatch = dispatch
          loggingArgs = LoggingViewModelArgs.none }

    /// <summary>
    /// Creates simple ViewModelArgs with no logging and no dispatch handling
    /// </summary>
    /// <param name="initialModel">The initial model state</param>
    /// <returns>ViewModelArgs with no-op dispatch and no logging</returns>
    let simple initialModel =
        createWithoutLogging initialModel ignore