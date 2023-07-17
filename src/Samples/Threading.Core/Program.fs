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

  let bindings () =
    [ "Pings" |> Binding.oneWay Pings.get
      "Message" |> Binding.twoWay (Message.get, UpdateMessage) ]

  let toCmd =
    function
    | DelayThenIncrementPings ->
      Elmish.Cmd.OfAsyncImmediate.perform (fun () -> Async.Sleep 1000) () (fun () -> IncrementPings)

let designVm = ViewModel.designInstance { Pings = 2; Message = "Hello" } (Program.bindings ())

let main window =

  let logger =
    LoggerConfiguration()
      .MinimumLevel.Override("Elmish.WPF.Update", Events.LogEventLevel.Verbose)
      .MinimumLevel.Override("Elmish.WPF.Bindings", Events.LogEventLevel.Verbose)
      .MinimumLevel.Override("Elmish.WPF.Performance", Events.LogEventLevel.Verbose)
      .WriteTo.Console()
      .CreateLogger()

  let program =
    WpfProgram.mkProgramWithCmdMsg (fun () -> Program.init) Program.update Program.bindings Program.toCmd
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