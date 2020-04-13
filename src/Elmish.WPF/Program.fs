[<RequireQualifiedAccess>]
module Elmish.WPF.Program

open System.Windows
open Elmish


/// Starts the Elmish dispatch loop, setting the bindings as the DataContext
/// for the specified FrameworkElement. Non-blocking. This is a low-level function;
/// for normal usage, see runWindow and runWindowWithConfig.
let startElmishLoop
    (config: ElmConfig)
    (element: FrameworkElement)
    (program: Program<unit, 'model, 'msg, Binding<'model, 'msg> list>) =
  let mutable lastModel = None

  let setState model dispatch =
    match lastModel with
    | None ->
        let bindings = Program.view program model dispatch
        let vm = ViewModel<'model,'msg>(model, dispatch, bindings, config, "main")
        element.DataContext <- vm
        lastModel <- Some vm
    | Some vm ->
        vm.UpdateModel model

  let uiDispatch (innerDispatch: Dispatch<'msg>) : Dispatch<'msg> =
    fun msg -> element.Dispatcher.Invoke(fun () -> innerDispatch msg)

  program
  |> Program.withSetState setState
  |> Program.withSyncDispatch uiDispatch
  |> Program.run


/// Starts the WPF dispatch loop. Will instantiate Application if it is not
/// already running, and then run the specified window. This is a blocking function.
let private startApp window =
  if isNull Application.Current then Application () |> ignore
  Application.Current.Run window


/// Starts the Elmish and WPF dispatch loops with the specified configuration.
/// Will instantiate Application if it is not already running, and then run the
/// specified window. This is a blocking function.
let runWindowWithConfig config window program =
  startElmishLoop config window program
  startApp window


/// Starts the Elmish and WPF dispatch loops. Will instantiate Application
/// if it is not already running, and then run the specified window. This is
/// a blocking function.
let runWindow window program =
  runWindowWithConfig ElmConfig.Default window program


/// Same as mkSimple, but with a signature adapted for Elmish.WPF.
let mkSimpleWpf
    (init: unit -> 'model)
    (update: 'msg  -> 'model -> 'model)
    (bindings: unit -> Binding<'model, 'msg> list) =
  Program.mkSimple init update (fun _ _ -> bindings ())


/// Same as mkProgram, but with a signature adapted for Elmish.WPF.
let mkProgramWpf
    (init: unit -> 'model * Cmd<'msg>)
    (update: 'msg  -> 'model -> 'model * Cmd<'msg>)
    (bindings: unit -> Binding<'model, 'msg> list) =
  Program.mkProgram init update (fun _ _ -> bindings ())


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
