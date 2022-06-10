namespace Elmish.WPF

open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Abstractions


type internal LoggingViewModelArgs =
  { performanceLogThresholdMs: int
    log: ILogger
    logPerformance: ILogger
    nameChain: string }

module internal LoggingViewModelArgs =

  let getNameChainFor nameChain name =
    sprintf "%s.%s" nameChain name

  let getNameChainForItem nameChain collectionBindingName itemId =
    sprintf "%s.%s.%s" nameChain collectionBindingName itemId

  let map nameChain v = { v with nameChain = nameChain }

  let none =
    { performanceLogThresholdMs = 1
      log = NullLogger.Instance
      logPerformance = NullLogger.Instance
      nameChain = "" }


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

  let map mapModel mapMsg v =
    { initialModel = v.initialModel |> mapModel
      dispatch = mapMsg >> v.dispatch
      loggingArgs = v.loggingArgs }

  let createWithoutLogging initialModel dispatch =
    { initialModel = initialModel
      dispatch = dispatch
      loggingArgs = LoggingViewModelArgs.none }
  
  let simple initialModel = createWithoutLogging initialModel ignore
