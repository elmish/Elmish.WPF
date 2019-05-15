[<RequireQualifiedAccess>]
module Elmish.WPF.Program

open System.Windows
open Elmish


/// Start Elmish dispatch loop
let internal startLoop
    (config: ElmConfig)
    (element: FrameworkElement)
    (programRun: Program<'t, 'model, 'msg, Binding<'model, 'msg> list> -> unit)
    (program: Program<'t, 'model, 'msg, Binding<'model, 'msg> list>) =
  let mutable lastModel = None

  let setState model dispatch =
    match lastModel with
    | None ->
        let mapping = Program.view program model dispatch
        let vm = ViewModel<'model,'msg>(model, dispatch, mapping, config, "main")
        element.DataContext <- box vm
        lastModel <- Some vm
    | Some vm ->
        vm.UpdateModel model

  let uiDispatch (innerDispatch: Dispatch<'msg>) : Dispatch<'msg> =
    fun msg -> element.Dispatcher.Invoke(fun () -> innerDispatch msg)

  program
  |> Program.withSetState setState
  |> Program.withSyncDispatch uiDispatch
  |> programRun


/// Start WPF dispatch loop. Blocking function.
let private startApp window =
  let app = if isNull Application.Current then Application() else Application.Current
  app.Run window


/// Starts both Elmish and WPF dispatch loops. Blocking function.
let runWindow window program =
  startLoop ElmConfig.Default window Elmish.Program.run program
  startApp window


/// Starts both Elmish and WPF dispatch loops with the specified configuration.
/// Blocking function.
let runWindowWithConfig config window program =
  startLoop config window Elmish.Program.run program
  startApp window
