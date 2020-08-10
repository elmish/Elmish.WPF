namespace Elmish.WPF

open System.Windows
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Abstractions
open Elmish


type WpfProgram<'model, 'msg> =
  internal {
    ElmishProgram: Program<unit, 'model, 'msg, Binding<'model, 'msg> list>
    LoggerFactory: ILoggerFactory
    /// Only log calls that take at least this many milliseconds. Default 1.
    PerformanceLogThreshold: int
  }


[<RequireQualifiedAccess>]
module WpfProgram =


  let private fromElmishProgram program =
    { ElmishProgram = program
      LoggerFactory = NullLoggerFactory.Instance
      PerformanceLogThreshold = 1 }


  /// Creates a WpfProgram that does not use commands.
  let mkSimple
      (init: unit -> 'model)
      (update: 'msg  -> 'model -> 'model)
      (bindings: unit -> Binding<'model, 'msg> list) =
    Program.mkSimple init update (fun _ _ -> bindings ())
    |> fromElmishProgram


  /// Creates a WpfProgram that uses commands
  let mkProgram
      (init: unit -> 'model * Cmd<'msg>)
      (update: 'msg  -> 'model -> 'model * Cmd<'msg>)
      (bindings: unit -> Binding<'model, 'msg> list) =
    Program.mkProgram init update (fun _ _ -> bindings ())
    |> fromElmishProgram


  /// Starts the Elmish dispatch loop, setting the bindings as the DataContext
  /// for the specified FrameworkElement. Non-blocking. This is a low-level function;
  /// for normal usage, see runWindow and runWindowWithConfig.
  let startElmishLoop
      (element: FrameworkElement)
      (program:  WpfProgram<'model, 'msg>) =
    let mutable lastModel = None

    let updateLogger = program.LoggerFactory.CreateLogger("Elmish.WPF.Update")
    let bindingsLogger = program.LoggerFactory.CreateLogger("Elmish.WPF.Bindings")
    let performanceLogger = program.LoggerFactory.CreateLogger("Elmish.WPF.Performance")

    let setState model dispatch =
      match lastModel with
      | None ->
          let bindings = Program.view program.ElmishProgram model dispatch
          let vm = ViewModel<'model, 'msg>(model, dispatch, bindings, program.PerformanceLogThreshold, "main", bindingsLogger, performanceLogger)
          element.DataContext <- vm
          lastModel <- Some vm
      | Some vm ->
          vm.UpdateModel model

    let uiDispatch (innerDispatch: Dispatch<'msg>) : Dispatch<'msg> =
      fun msg -> element.Dispatcher.Invoke(fun () -> innerDispatch msg)

    let logMsgAndModel (msg: 'msg) (model: 'model) = 
      updateLogger.LogTrace("New message: {Message}", msg)
      updateLogger.LogTrace("Updated state:\n{Model}", model)

    let logError (msg: string, ex: exn) =
      updateLogger.LogError(ex, msg)

    program.ElmishProgram
    |> if updateLogger.IsEnabled(LogLevel.Trace) then Program.withTrace logMsgAndModel else id
    |> Program.withErrorHandler logError
    |> Program.withSetState setState
    |> Program.withSyncDispatch uiDispatch
    |> Program.run


  /// Instantiates Application and sets its MainWindow if it is not already
  /// running.
  let private initializeApplication window =
    if isNull Application.Current then
      Application () |> ignore
      Application.Current.MainWindow <- window


  /// Starts the Elmish and WPF dispatch loops. Will instantiate Application and
  /// set its MainWindow if it is not already running, and then run the specified
  /// window. This is a blocking function.
  let runWindow window program =
    initializeApplication window
    window.Show ()
    startElmishLoop window program
    Application.Current.Run window


  /// Same as mkProgram, except that init and update doesn't return Cmd<'msg>
  /// directly, but instead return a CmdMsg discriminated union that is converted
  /// to Cmd<'msg> using toCmd. This means that the init and update functions
  /// return only data, and thus are easier to unit test. The CmdMsg pattern is
  /// general; this is just a trivial convenience function that automatically
  /// converts CmdMsg to Cmd<'msg> for you in init and update
  let mkProgramWithCmdMsg
      (init: unit -> 'model * 'cmdMsg list)
      (update: 'msg -> 'model -> 'model * 'cmdMsg list)
      (bindings: unit -> Binding<'model, 'msg> list)
      (toCmd: 'cmdMsg -> Cmd<'msg>) =
    let convert (model, cmdMsgs) =
      model, (cmdMsgs |> List.map toCmd |> Cmd.batch)
    mkProgram
      (init >> convert)
      (fun msg model -> update msg model |> convert)
      bindings


  /// Uses the specified ILoggerFactory for logging.
  let withLogger loggerFactory program =
    { program with LoggerFactory = loggerFactory }


  /// Subscribe to an external source of events. The subscribe function is called once,
  /// with the initial model, but can dispatch messages at any time.
  let withSubscription subscribe program =
    { program with ElmishProgram = program.ElmishProgram |> Program.withSubscription subscribe }


  /// Only logs binding performance for calls taking longer than the specified number of
  /// milliseconds. The default is 1ms.
  let withPerformanceLogThreshold threshold program =
    { program with PerformanceLogThreshold = threshold }
