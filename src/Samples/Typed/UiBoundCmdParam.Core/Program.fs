module Elmish.WPF.Samples.UiBoundCmdParam.Program

open Serilog
open Serilog.Extensions.Logging
open Elmish.WPF

type Model =
    { Numbers: int list
      EnabledMaxLimit: int }

let init () =
    { Numbers = [ 0..10 ]
      EnabledMaxLimit = 5 }

type Msg =
    | SetLimit of int
    | Command

let update msg m =
    match msg with
    | SetLimit x -> { m with EnabledMaxLimit = x }
    | Command -> m

[<AllowNullLiteral>]
type UiBoundCmdParamViewModel(args) =
    inherit ViewModelBase<Model, Msg>(args)

    let limitBinding =
        Binding.TwoWayT.id
        >> Binding.addLazy (=)
        >> Binding.mapModel (fun m -> float m.EnabledMaxLimit)
        >> Binding.mapMsg (int >> SetLimit)

    member _.Numbers =
        base.Get
            ()
            (Binding.OneWayT.id
             >> Binding.addLazy (=)
             >> Binding.mapModel (fun m -> m.Numbers))

    member this.Limit
        with get () = base.Get () limitBinding
        and set (value) = base.Set (value) limitBinding

    member _.Command =
        base.Get
            ()
            (Binding.CmdT.id true (fun (p: obj) m -> not (isNull p) && p :?> int <= m.EnabledMaxLimit)
             >> Binding.mapMsg (fun _ -> Command))

let designVm = UiBoundCmdParamViewModel(ViewModelArgs.simple (init ()))

let main window =
    let logger =
        LoggerConfiguration()
            .MinimumLevel.Override("Elmish.WPF.Update", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Bindings", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Performance", Events.LogEventLevel.Verbose)
            .WriteTo.Console()
            .CreateLogger()

    let createVm args = UiBoundCmdParamViewModel(args)

    WpfProgram.mkSimpleT init update createVm
    |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))
    |> WpfProgram.startElmishLoop window