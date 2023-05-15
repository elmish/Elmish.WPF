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
      (createVm: ViewModelArgs<'model, 'msg> -> 'viewModel) =
    Program.mkSimple init update (fun _ _ -> ())
    |> createWithVm createVm


  /// Creates a WpfProgram that uses commands
  let mkProgramT
      (init: unit -> 'model * Cmd<'msg>)
      (update: 'msg  -> 'model -> 'model * Cmd<'msg>)
      (createVm: ViewModelArgs<'model, 'msg> -> 'viewModel) =
    Program.mkProgram init update (fun _ _ -> ())
    |> createWithVm createVm

  [<Struct>]
  type ElmishThreaderBehavior =
  | SingleThreaded
  | Threaded_NoUIDispatch
  | Threaded_PendingUIDispatch of pending: System.Threading.Tasks.TaskCompletionSource<unit -> unit>
  | Threaded_UIDispatch of active: System.Threading.Tasks.TaskCompletionSource<unit -> unit>

  /// <summary>Starts an Elmish dispatch loop, setting the bindings as the DataContext for the
  /// specified FrameworkElement. Non-blocking. If you have an explicit entry point where
  /// you control app/window instantiation, runWindowWithConfig might be a better option.
  ///
  /// If you execute this from a thread other than the thread owning element.Dispatcher (UI Thread),
  /// Elmish.WPF will use that background thread to run updates rather than the main UI thread.</summary>
  /// <remarks>Example multithreaded use:
  /// <code><![CDATA[
  /// let elmishThread =
  ///   Thread(
  ///     ThreadStart(fun () ->
  ///       WpfProgram.startElmishLoop window program
  ///       Dispatcher.Run()))
  /// elmishThread.Name <- "ElmishDispatchThread"
  /// elmishThread.Run()
  ///
  /// mainWindow.Show()
  /// let result = Application.Current.Run mainWindow
  ///
  /// Threading.Dispatcher.FromThread(elmishThread).InvokeShutdown()
  /// elmishThread.Join()
  /// ]]></code></remarks>
  /// <param name="element"></param>
  /// <param name="program"></param>
  /// <returns></returns>
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

    // Dispatch that comes in from a view model message (setter or WPF ICommand). These may come from UI thread, so must be streated specially
    let dispatchFromViewModel msg =
      if element.Dispatcher = Threading.Dispatcher.CurrentDispatcher then // if the message is from the UI thread
        match threader with
        | SingleThreaded -> dispatch msg // Dispatch directly if `elmishDispatcher` is the same as the UI thread
        | Threaded_NoUIDispatch -> // If `elmishDispatcher` is different, invoke dispatch on it then wait around for it to finish executing, then execute the continuation on the current (UI) thread
          let uiWaiter = System.Threading.Tasks.TaskCompletionSource<unit -> unit>()
          threader <- Threaded_PendingUIDispatch uiWaiter

          // This should always leave `threader` in the `Threaded_NoUIDispatch` state before leaving this thread invocation
          let synchronizedUiDispatch () =
            threader <- Threaded_UIDispatch uiWaiter
            dispatch msg
            threader <- Threaded_NoUIDispatch

          elmishDispatcher.InvokeAsync(synchronizedUiDispatch) |> ignore
          // Wait on `elmishDispatcher` to get to this invocation and collect result
          let continuationOnUIThread = uiWaiter.Task.Result
          // Result is the `program.UpdateViewModel` call, so execute here on the UI thread
          continuationOnUIThread()
        | Threaded_PendingUIDispatch uiWaiter
        | Threaded_UIDispatch uiWaiter ->
          uiWaiter.SetException(exn("Error in core Elmish.WPF threading code. Invalid state reached!"))
      else // message is not from the UI thread
        elmishDispatcher.InvokeAsync(fun () -> dispatch msg) |> ignore // handle as a command message

    // Core Elmish calls this from `dispatch`, which means this is always called from `elmishDispatcher`
    // (which is UI thread in single-threaded case)
    let setUiState model _syncDispatch =
      match viewModel with
      | None -> // no view model yet, so create one
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
      | Some vm -> // view model exists, so update
          match threader with
          | Threaded_UIDispatch uiWaiter -> // We are in the specific dispatch call from the UI thread (see `synchronizedUiDispatch` in `dispatchFromViewModel`)
            uiWaiter.SetResult(fun () -> program.UpdateViewModel (vm, model)) // execute `UpdateViewModel` on UI thread
          | Threaded_PendingUIDispatch _ -> // We are in a non-UI dispatch that updated the model before the UI got its update in, but after the user interacted
            () // Skip updating the UI since the screen is frozen anyways, and `program.UpdateViewModel` is fully transitive
          | Threaded_NoUIDispatch -> // We are in a non-UI dispatch with no pending user interactions known
            element.Dispatcher.InvokeAsync(fun () -> program.UpdateViewModel (vm, model)) |> ignore // Schedule update normally
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

    let logMsgAndModel (msg: 'msg) (model: 'model) _ =
      updateLogger.LogTrace("New message: {Message}\nUpdated state:\n{Model}", msg, model)

    let errorHandler (msg: string, ex: exn) =
      updateLogger.LogError(ex, msg)
      program.ErrorHandler msg ex

    program.ElmishProgram
    |> if updateLogger.IsEnabled LogLevel.Trace then Program.withTrace logMsgAndModel else id
    |> Program.withErrorHandler errorHandler
    |> Program.withSetState setUiState
    |> Program.runWithDispatch cmdDispatch ()


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


  /// Same as mkProgramT, except that init and update don't return Cmd<'msg>
  /// directly, but instead return a CmdMsg discriminated union that is converted
  /// to Cmd<'msg> using toCmd. This means that the init and update functions
  /// return only data, and thus are easier to unit test. The CmdMsg pattern is
  /// general; this is just a trivial convenience function that automatically
  /// converts CmdMsg to Cmd<'msg> for you in init and update.
  let mkProgramWithCmdMsgT
      (init: unit -> 'model * 'cmdMsg list)
      (update: 'msg -> 'model -> 'model * 'cmdMsg list)
      (createVm: ViewModelArgs<'model, 'msg> -> 'viewModel)
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


  /// Subscribe to external source of events, overrides existing subscription.
  /// Return the subscriptions that should be active based on the current model.
  /// Subscriptions will be started or stopped automatically to match.
  let withSubscription (subscribe: 'model -> Sub<'msg>) program =
    { program with ElmishProgram = program.ElmishProgram |> Program.withSubscription subscribe }


  /// Map existing subscription to external source of events.
  let mapSubscription map program =
      { program with ElmishProgram = program.ElmishProgram |> Program.mapSubscription map }


  /// Only logs binding performance for calls taking longer than the specified number of
  /// milliseconds. The default is 1ms.
  let withPerformanceLogThreshold threshold program =
    { program with PerformanceLogThreshold = threshold }


[<RequireQualifiedAccess>]
module Subscribe =

  let ofEffect dispose (effect: Effect<'msg>) : Subscribe<'msg> =
    fun dispatch ->
      effect dispatch
      { new System.IDisposable with member _.Dispose() = dispose () }


[<RequireQualifiedAccess>]
module Sub =
  /// Subscribe to an external source of events. The subscribe function is called once,
  /// with the initial model, but can dispatch messages at any time.
  [<System.Obsolete("Migrate your v3 subscriptions to the new subscriptions with lifetimes and dispose")>]
  let fromV3Subscription (idPrefix: string) (v3Subscription: 'model -> Cmd<'msg>) : 'model -> Sub<'msg> =
    let mutable memoizedSub : Sub<'msg> voption = ValueNone

    fun model ->
      match memoizedSub with
      | ValueNone ->
        let sub =
          v3Subscription model
          |> List.map (Subscribe.ofEffect id)
          |> List.indexed
          |> List.map (fun (i, subscribe) ->
            [ idPrefix; string i ], subscribe)
        memoizedSub <- ValueSome sub
        sub
      | ValueSome sub -> sub
