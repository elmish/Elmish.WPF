namespace Elmish.WPF

open System.Windows
open Elmish


type ProgramWpf<'arg, 'model, 'msg> = private {
  ElmishProgram: Program<'arg, 'model, 'msg, Binding<'model, 'msg> list>
  ElmConfig: ElmConfig
}


[<RequireQualifiedAccess>]
module Program =

  /// Starts the Elmish dispatch loop, setting the bindings as the DataContext
  /// for the specified FrameworkElement. Non-blocking. This is a low-level function;
  /// for normal usage, see runWindow and runWindowWithConfig.
  let startElmishLoop
      (element: FrameworkElement)
      (program: ProgramWpf<unit, 'model, 'msg>) =
    let mutable lastModel = None
  
    let setState model dispatch =
      match lastModel with
      | None ->
          let bindings = Program.view program.ElmishProgram model dispatch
          let vm = ViewModel<'model,'msg>(model, dispatch, bindings, program.ElmConfig, "main")
          element.DataContext <- vm
          lastModel <- Some vm
      | Some vm ->
          vm.UpdateModel model
  
    let uiDispatch (innerDispatch: Dispatch<'msg>) : Dispatch<'msg> =
      fun msg -> element.Dispatcher.Invoke(fun () -> innerDispatch msg)
  
    program.ElmishProgram
    |> Program.withSetState setState
    |> Program.withSyncDispatch uiDispatch
    |> Program.run
  
  
  /// Instantiates Application and sets its MainWindow if it is not already
  /// running.
  let private initializeApplication window =
    if isNull Application.Current then
      Application () |> ignore
      Application.Current.MainWindow <- window
  
  
  /// Starts the Elmish and WPF dispatch loops.  Will instantiate Application
  /// and set its MainWindow if it is not already running, and then run the
  /// specified window. This is a blocking function.
  let runWindow (window: Window) program =
    initializeApplication window
    window.Show ()
    startElmishLoop window program
    Application.Current.Run window
  
  
  /// Same as mkSimple, but with a signature adapted for Elmish.WPF.
  let mkSimpleWpf
      (init: unit -> 'model)
      (update: 'msg  -> 'model -> 'model)
      (bindings: unit -> Binding<'model, 'msg> list) =
    { ElmishProgram = Program.mkSimple init update (fun _ _ -> bindings ())
      ElmConfig = ElmConfig.Default }
  
  
  /// Same as mkProgram, but with a signature adapted for Elmish.WPF.
  let mkProgramWpf
      (init: unit -> 'model * Cmd<'msg>)
      (update: 'msg  -> 'model -> 'model * Cmd<'msg>)
      (bindings: unit -> Binding<'model, 'msg> list) =
    { ElmishProgram = Program.mkProgram init update (fun _ _ -> bindings ())
      ElmConfig = ElmConfig.Default }
  
  
  /// Same as mkProgramWpf, except that init and update doesn't return Cmd<'msg>
  /// directly, but instead return a CmdMsg discriminated union that is converted
  /// to Cmd<'msg> using toCmd. This means that the init and update functions
  /// return only data, and thus are easier to unit test. The CmdMsg pattern is
  /// general; this is just a trivial convenience function that automatically
  /// converts CmdMsg to Cmd<'msg> for you in inint and update
  let mkProgramWpfWithCmdMsg
      (init: unit -> 'model * 'cmdMsg list)
      (update: 'msg -> 'model -> 'model * 'cmdMsg list)
      (bindings: unit -> Binding<'model, 'msg> list)
      (toCmd: 'cmdMsg -> Cmd<'msg>) =
    let convert (model, cmdMsgs) =
      model, (cmdMsgs |> List.map toCmd |> Cmd.batch)
    mkProgramWpf
      (init >> convert)
      (fun msg model -> update msg model |> convert)
      bindings
  
  
  /// Traces all updates using System.Diagnostics.Debug.WriteLine.
  let withDebugTrace program =
    program |> Program.withTrace (fun msg model ->
      System.Diagnostics.Debug.WriteLine(sprintf "New message: %A" msg)
      System.Diagnostics.Debug.WriteLine(sprintf "Updated state: %A" model)
    )

  let mapElmishProgram f program =
    { program with ElmishProgram = f program.ElmishProgram}

  let logConsole program =
    { program with ElmConfig = { program.ElmConfig with LogConsole = true } }

  let logTrace program =
    { program with ElmConfig = { program.ElmConfig with LogTrace = true } }

  let measure program =
    { program with ElmConfig = { program.ElmConfig with Measure = true } }

  let withMeasureLimitMs i program =
    { program with ElmConfig = { program.ElmConfig with MeasureLimitMs = i } }
