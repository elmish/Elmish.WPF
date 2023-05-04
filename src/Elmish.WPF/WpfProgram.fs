namespace Elmish.WPF

open System.Windows
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Abstractions
open Elmish


type WpfProgram<'model, 'msg, 'viewModel> =
  internal {
    ElmishProgram: Program<unit, 'model, 'msg, unit>
    CreateViewModel: ViewModelArgs<'model,'msg> -> 'viewModel
    UpdateViewModel: 'viewModel * 'model -> unit
    LoggerFactory: ILoggerFactory
    ErrorHandler: string -> exn -> unit
    /// Only log calls that take at least this many milliseconds. Default 1.
    PerformanceLogThreshold: int
  }

type WpfProgram<'model, 'msg> = WpfProgram<'model, 'msg, obj>


[<RequireQualifiedAccess>]
module WpfProgram =

  let private mapVm fOut fIn (p: WpfProgram<'model, 'msg, 'viewModel0>) : WpfProgram<'model, 'msg, 'viewModel1> =
    { ElmishProgram = p.ElmishProgram
      CreateViewModel = p.CreateViewModel >> fOut
      UpdateViewModel = (fun (vm, m) -> p.UpdateViewModel(fIn vm, m))
      LoggerFactory = p.LoggerFactory
      ErrorHandler = p.ErrorHandler
      PerformanceLogThreshold = p.PerformanceLogThreshold }

  let private createWithBindings (getBindings: unit -> Binding<'model,'msg> list) program =
    { ElmishProgram = program
      CreateViewModel = fun args -> DynamicViewModel<'model,'msg>(args, getBindings ())
      UpdateViewModel = IViewModel.updateModel
      LoggerFactory = NullLoggerFactory.Instance
      ErrorHandler = fun _ _ -> ()
      PerformanceLogThreshold = 1 }
    |> mapVm box unbox

  let private createWithVm (createVm: ViewModelArgs<'model, 'msg> -> #IViewModel<'model, 'msg>) program =
    { ElmishProgram = program
      CreateViewModel = createVm
      UpdateViewModel = IViewModel.updateModel
      LoggerFactory = NullLoggerFactory.Instance
      ErrorHandler = fun _ _ -> ()
      PerformanceLogThreshold = 1 }


  /// Creates a WpfProgram that does not use commands.
  let mkSimple
      (init: unit -> 'model)
      (update: 'msg  -> 'model -> 'model)
      (bindings: unit -> Binding<'model, 'msg> list) =
    Program.mkSimple init update (fun _ _ -> ())
    |> createWithBindings bindings


  /// Creates a WpfProgram that uses commands
  let mkProgram
      (init: unit -> 'model * Cmd<'msg>)
      (update: 'msg  -> 'model -> 'model * Cmd<'msg>)
      (bindings: unit -> Binding<'model, 'msg> list) =
    Program.mkProgram init update (fun _ _ -> ())
    |> createWithBindings bindings

  /// Creates a WpfProgram that does not use commands.
  let mkSimpleT
      (init: unit -> 'model)
      (update: 'msg  -> 'model -> 'model)
      (createVm: ViewModelArgs<'model, 'msg> -> #IViewModel<'model, 'msg>) =
    Program.mkSimple init update (fun _ _ -> ())
    |> createWithVm createVm


  /// Creates a WpfProgram that uses commands
  let mkProgramT
      (init: unit -> 'model * Cmd<'msg>)
      (update: 'msg  -> 'model -> 'model * Cmd<'msg>)
      (createVm: ViewModelArgs<'model, 'msg> -> #IViewModel<'model, 'msg>) =
    Program.mkProgram init update (fun _ _ -> ())
    |> createWithVm createVm

  [<Struct>]
  type ElmishThreaderBehavior =
  | SingleThreaded
  | Threaded_NoUIDispatch
  | Threaded_PendingUIDispatch of pending: System.Threading.Tasks.TaskCompletionSource<unit -> unit>
  | Threaded_UIDispatch of active: System.Threading.Tasks.TaskCompletionSource<unit -> unit>

  /// Starts an Elmish dispatch loop, setting the bindings as the DataContext for the
  /// specified FrameworkElement. Non-blocking. If you have an explicit entry point where
  /// you control app/window instantiation, runWindowWithConfig might be a better option.
  let startElmishLoop
      (element: FrameworkElement)
      (program: WpfProgram<'model, 'msg, 'viewModel>) =
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

    let elmishDispatcher = Threading.Dispatcher.CurrentDispatcher
    let mutable threader =
      if element.Dispatcher = elmishDispatcher then
        SingleThreaded
      else
        Threaded_NoUIDispatch

    // Always must be synchronized with elmishDispatcher (or UI thread in single-threaded case)
    let setUiState model _syncDispatch =
      match viewModel with
      | None -> // no view model yet, so create one
          let dispatchFromViewModel msg =
            if element.Dispatcher = Threading.Dispatcher.CurrentDispatcher then // if the message is from the UI thread
              match threader with
              | SingleThreaded -> dispatch msg
              | Threaded_NoUIDispatch ->
                let uiWaiter = System.Threading.Tasks.TaskCompletionSource<unit -> unit>()
                threader <- Threaded_PendingUIDispatch uiWaiter

                let synchronizedUiDispatch () =
                  threader <- Threaded_UIDispatch uiWaiter
                  dispatch msg
                  threader <- Threaded_NoUIDispatch

                elmishDispatcher.InvokeAsync(synchronizedUiDispatch) |> ignore
                let continuationOnUIThread = uiWaiter.Task.Result
                continuationOnUIThread()
              | Threaded_PendingUIDispatch uiWaiter
              | Threaded_UIDispatch uiWaiter ->
                uiWaiter.SetException(exn("Error in core Elmish.WPF threading code. Invalid state reached!"))
            else // message is not from the UI thread
              elmishDispatcher.InvokeAsync(fun () -> dispatch msg) |> ignore // handle as a command message
          let args =
            { initialModel = model
              dispatch = dispatchFromViewModel
              loggingArgs =
                { performanceLogThresholdMs = program.PerformanceLogThreshold
                  nameChain = "main"
                  log = bindingsLogger
                  logPerformance = performanceLogger } }
          let vm = program.CreateViewModel args
          element.Dispatcher.Invoke(fun () -> element.DataContext <- vm)
          viewModel <- Some vm
      | Some vm ->
          match threader with
          | Threaded_UIDispatch uiWaiter ->
            uiWaiter.SetResult(fun () -> program.UpdateViewModel (vm, model)) // execute `UpdateViewModel` on UI thread
          | Threaded_PendingUIDispatch _ ->
            () // Skip updating the UI if we aren't at the update that does the UI yet, but have one pending
          | Threaded_NoUIDispatch -> // If there are no pending updates from dispatchFromViewModel, schedule update normally
            element.Dispatcher.InvokeAsync(fun () -> program.UpdateViewModel (vm, model)) |> ignore
          | SingleThreaded -> // If we aren't using different threads, always process normally
            element.Dispatcher.Invoke(fun () -> program.UpdateViewModel (vm, model))

    let cmdDispatch (innerDispatch: Dispatch<'msg>) : Dispatch<'msg> =
      dispatch <- innerDispatch
      (*
       * Have commands asynchronously dispatch messages.
       * This avoids race conditions like those that can occur when shutting down.
       * https://github.com/elmish/Elmish.WPF/issues/353
       *)
      fun msg -> elmishDispatcher.InvokeAsync(fun () -> dispatch msg) |> ignore

    let logMsgAndModel (msg: 'msg) (model: 'model) =
      updateLogger.LogTrace("New message: {Message}\nUpdated state:\n{Model}", msg, model)

    let errorHandler (msg: string, ex: exn) =
      updateLogger.LogError(ex, msg)
      program.ErrorHandler msg ex

    program.ElmishProgram
    |> if updateLogger.IsEnabled LogLevel.Trace then Program.withTrace logMsgAndModel else id
    |> Program.withErrorHandler errorHandler
    |> Program.withSetState setUiState
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


  /// Same as mkProgram2, except that init and update don't return Cmd<'msg>
  /// directly, but instead return a CmdMsg discriminated union that is converted
  /// to Cmd<'msg> using toCmd. This means that the init and update functions
  /// return only data, and thus are easier to unit test. The CmdMsg pattern is
  /// general; this is just a trivial convenience function that automatically
  /// converts CmdMsg to Cmd<'msg> for you in init and update.
  let mkProgramWithCmdMsgT
      (init: unit -> 'model * 'cmdMsg list)
      (update: 'msg -> 'model -> 'model * 'cmdMsg list)
      (createVm: ViewModelArgs<'model, 'msg> -> #IViewModel<'model, 'msg>)
      (toCmd: 'cmdMsg -> Cmd<'msg>) =
    let convert (model, cmdMsgs) =
      model, (cmdMsgs |> List.map toCmd |> Cmd.batch)
    mkProgramT
      (init >> convert)
      (fun msg model -> update msg model |> convert)
      createVm


  /// Uses the specified ILoggerFactory for logging.
  let withLogger loggerFactory program =
    { program with LoggerFactory = loggerFactory }


  /// Calls the specified function for unhandled exceptions in the Elmish
  /// dispatch loop (e.g. in commands or the update function). This essentially
  /// delegates to Elmish's Program.withErrorHandler.
  ///
  /// The first (string) argument of onError is a message from Elmish describing
  /// the context of the exception. Note that this may contain a rendered
  /// message case with all data ("%A" formatting).
  ///
  /// Note that exceptions passed to onError are also logged to the logger
  /// specified using WpfProgram.withLogger.
  let withElmishErrorHandler onError program =
    { program with ErrorHandler = onError }


  /// Subscribe to an external source of events. The subscribe function is called once,
  /// with the initial model, but can dispatch messages at any time.
  let withSubscription subscribe program =
    { program with ElmishProgram = program.ElmishProgram |> Program.withSubscription subscribe }


  /// Only logs binding performance for calls taking longer than the specified number of
  /// milliseconds. The default is 1ms.
  let withPerformanceLogThreshold threshold program =
    { program with PerformanceLogThreshold = threshold }
