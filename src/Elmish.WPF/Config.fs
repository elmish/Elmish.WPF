namespace Elmish.WPF

type ElmConfig =
  { // Whether to log to console. Default false.
    LogConsole: bool
    // Whether to log to trace (VS debug output). Default false.
    LogTrace: bool
    // Whether to measure and log calls to functions supplied in bindings. Default false.
    Measure: bool
    // If Measure is true, only log calls that take at least this many milliseconds. Default 1.
    MeasureLimitMs: int
  }
  static member Default =
    { LogConsole = false
      LogTrace = false
      Measure = false
      MeasureLimitMs = 1 }
