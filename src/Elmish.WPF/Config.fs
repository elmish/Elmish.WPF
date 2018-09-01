namespace Elmish.WPF

type ElmConfig =
  { LogConsole: bool
    LogTrace: bool }
  static member Default =
    { LogConsole = false
      LogTrace = false }
