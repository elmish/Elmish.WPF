module Elmish.WPF.Samples.UiBoundCmdParam.Program

open Serilog
open Serilog.Extensions.Logging
open Elmish.WPF


type Model =
  { Numbers: int list
    EnabledMaxLimit: int }

let init () =
  { Numbers = [0 .. 10]
    EnabledMaxLimit = 5 }

type Msg =
  | SetLimit of int
  | Command

let update msg m =
  match msg with
  | SetLimit x -> { m with EnabledMaxLimit = x }
  | Command -> m

let bindings () : Binding<Model, Msg> list = [
  "Numbers" |> Binding.oneWay(fun m -> m.Numbers)
  "Limit" |> Binding.twoWay((fun m -> float m.EnabledMaxLimit), int >> SetLimit)
  "Command" |> Binding.cmdParamIf(
    (fun p m -> Command),
    (fun p m -> not (isNull p) && p :?> int <= m.EnabledMaxLimit),
    true)
]

let designVm = ViewModel.designInstance (init ()) (bindings ())

let main window =

  Log.Logger <- 
    LoggerConfiguration()
      .MinimumLevel.Override("Elmish.Messages", Events.LogEventLevel.Verbose)
      .MinimumLevel.Override("Elmish.State", Events.LogEventLevel.Verbose)
      .MinimumLevel.Override("Elmish.WPF.Bindings", Events.LogEventLevel.Verbose)
      .MinimumLevel.Override("Elmish.WPF.Performance", Events.LogEventLevel.Verbose)
      .WriteTo.Console()
      .CreateLogger()

  WpfProgram.mkSimple init update bindings
  |> WpfProgram.withLogger (new SerilogLoggerFactory())
  |> WpfProgram.runWindow window
