namespace Elmish.WPF

open System.Windows
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Abstractions
open Elmish


type WpfProgram<'model, 'msg> =
  internal {
    ElmishProgram: Program<unit, 'model, 'msg, unit>
    Bindings: Binding<'model, 'msg> list
    LoggerFactory: ILoggerFactory
    /// Only log calls that take at least this many milliseconds. Default 1.
    PerformanceLogThreshold: int
  }


[<RequireQualifiedAccess>]
module WpfProgram =


  let private create getBindings program =
    { ElmishProgram = program
      Bindings = getBindings ()
      LoggerFactory = NullLoggerFactory.Instance
      PerformanceLogThreshold = 1 }


  /// Creates a WpfProgram that does not use commands.
  let mkSimple
      (init: unit -> 'model)
      (update: 'msg  -> 'model -> 'model)
      (bindings: unit -> Binding<'model, 'msg> list) =
    Program.mkSimple init update (fun _ _ -> ())
    |> create bindings


  /// Creates a WpfProgram that uses commands
  let mkProgram
      (init: unit -> 'model * Cmd<'msg>)
      (update: 'msg  -> 'model -> 'model * Cmd<'msg>)
      (bindings: unit -> Binding<'model, 'msg> list) =
    Program.mkProgram init update (fun _ _ -> ())
    |> create bindings


  /// Starts an Elmish dispatch loop, setting the bindings as the DataContext for the
  /// specified FrameworkElement. Non-blocking. If you have an explicit entry point where
  /// you control app/window instantiation, runWindowWithConfig might be a better option.
  let startElmishLoop
      (element: FrameworkElement)
      (program: WpfProgram<'model, 'msg>) =
    let mutable viewModel = None

    let updateLogger = program.LoggerFactory.CreateLogger("Elmish.WPF.Update")
    let bindingsLogger = program.LoggerFactory.CreateLogger("Elmish.WPF.Bindings")
    let performanceLogger = program.LoggerFactory.CreateLogger("Elmish.WPF.Performance")

    (*
     * Capture the dispatch function before wrapping it with Dispatcher.InvokeAsync
     * so that the UI can synchronously dispatch messages.
     * In additional to being slightly more efficient,
     * it also helps keep WPF in the correct state.
     * https://github.com/elmish/Elmish.WPF/issues/371
     * https://github.com/elmish/Elmish.WPF/issues/373
     *
     * This is definitely a hack.
     * Maybe something with Elmish can change so this hack can be avoided.
     *)
    let mutable dispatch = Unchecked.defaultof<Dispatch<'msg>>

    let setState model _ =
      match viewModel with
      | None ->
          let uiDispatch msg = element.Dispatcher.Invoke(fun () -> dispatch msg)
          let vm = ViewModel<'model, 'msg>(model, uiDispatch, program.Bindings, program.PerformanceLogThreshold, "main", bindingsLogger, performanceLogger)
          element.DataContext <- vm
          viewModel <- Some vm
      | Some vm ->
          vm.UpdateModel model

    let cmdDispatch (innerDispatch: Dispatch<'msg>) : Dispatch<'msg> =
      dispatch <- innerDispatch
      (*
       * Have commands asynchronously dispatch messages.
       * This avoids race conditions like those that can occur when shutting down.
       * https://github.com/elmish/Elmish.WPF/issues/353
       *)
      fun msg -> element.Dispatcher.InvokeAsync(fun () -> innerDispatch msg) |> ignore

    let logMsgAndModel (msg: 'msg) (model: 'model) =
      updateLogger.LogTrace("New message: {Message}\nUpdated state:\n{Model}", msg, model)

    let logError (msg: string, ex: exn) =
      updateLogger.LogError(ex, msg)

    program.ElmishProgram
    |> if updateLogger.IsEnabled LogLevel.Trace then Program.withTrace logMsgAndModel else id
    |> Program.withErrorHandler logError
    |> Program.withSetState setState
    |> Program.withSyncDispatch cmdDispatch
    |> Program.run


  /// Instantiates Application and sets its MainWindow if it is not already
  /// running.
  let private initializeApplication window =
    if isNull Application.Current then
      Application () |> ignore
      Application.Current.MainWindow <- window


  /// Starts the Elmish and WPF dispatch loops. Will instantiate Application and set its
  /// MainWindow if it is not already running, and then run the specified window. This is a
  /// blocking function. If you are using App.xaml as an implicit entry point, see
  /// startElmishLoop.
  let runWindow window program =
    (*
     * This is the correct order for these four statements.
     * 1. Initialize Application.Current and set its MainWindow in case the
     *    user code accesses either of these when initializing the bindings.
     * 2. Start the Elmish loop, which will cause the main view model to be
     *    created and assigned to the window's DataContext before returning.
     * 3. Show the window now that the DataContext is set.
     * 4. Run the current application, which must be last because it is blocking.
     *)
    initializeApplication window
    startElmishLoop window program
    window.Show ()
    Application.Current.Run window


  /// Same as mkProgram, except that init and update don't return Cmd<'msg>
  /// directly, but instead return a CmdMsg discriminated union that is converted
  /// to Cmd<'msg> using toCmd. This means that the init and update functions
  /// return only data, and thus are easier to unit test. The CmdMsg pattern is
  /// general; this is just a trivial convenience function that automatically
  /// converts CmdMsg to Cmd<'msg> for you in init and update.
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
