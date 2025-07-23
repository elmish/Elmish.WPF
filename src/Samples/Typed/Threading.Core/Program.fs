module Elmish.WPF.Samples.Threading.Program

open System.Threading
open System.Windows.Threading

open Serilog
open Serilog.Extensions.Logging

open Elmish.WPF


type Model = { Pings: int; Message: string }

type Msg =
    | IncrementPings
    | UpdateMessage of string
    | AppendPingsToMessage

type Cmd = | DelayThenIncrementPings


module Program =
    module Pings =
        let get m = m.Pings
        let set v m = { m with Pings = v }
        let map f m = m |> get |> f |> set <| m

    module Message =
        let get m = m.Message
        let set v m = { m with Message = v }

    let init = { Pings = 0; Message = "" }, [ DelayThenIncrementPings ]

    let update msg m =
        match msg with
        | IncrementPings -> m |> Pings.map ((+) 1), [ DelayThenIncrementPings ]
        | UpdateMessage message -> m |> Message.set message, []
        | AppendPingsToMessage ->
            { m with
                Message = m.Message + string m.Pings },
            []

    let toCmd =
        function
        | DelayThenIncrementPings ->
            Elmish.Cmd.OfAsyncImmediate.perform (fun () -> Async.Sleep 1000) () (fun () -> IncrementPings)


[<AllowNullLiteral>]
type ThreadingViewModel(args) =
    inherit ViewModelBase<Model, Msg>(args)

    let messageBinding =
        Binding.TwoWayT.id
        >> Binding.addLazy (=)
        >> Binding.mapModel (fun m -> m.Message)
        >> Binding.mapMsg UpdateMessage

    member _.Pings =
        base.Get () (Binding.OneWayT.id >> Binding.addLazy (=) >> Binding.mapModel (fun m -> m.Pings))

    member this.Message
        with get () = base.Get () messageBinding
        and set (value) = base.Set (value) messageBinding

    member _.AppendPingsToMessage =
        base.Get () (Binding.CmdT.setAlways AppendPingsToMessage)


let designVm =
    ThreadingViewModel(ViewModelArgs.simple { Pings = 2; Message = "Hello" })

let main window =

    let logger =
        LoggerConfiguration()
            .MinimumLevel.Override("Elmish.WPF.Update", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Bindings", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Performance", Events.LogEventLevel.Verbose)
            .WriteTo.Console()
            .CreateLogger()

    let createVm args = ThreadingViewModel(args)

    let program =
        WpfProgram.mkProgramWithCmdMsgT (fun () -> Program.init) Program.update createVm Program.toCmd
        |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))

    let elmishThread =
        Thread(
            ThreadStart(fun () ->
                WpfProgram.startElmishLoop window program
                Dispatcher.Run())
        )

    elmishThread.Name <- "ElmishDispatchThread"
    elmishThread.Start()

    elmishThread