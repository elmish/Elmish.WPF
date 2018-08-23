[<RequireQualifiedAccess>]
module Elmish.WPF.Program

open System.Windows
open Elmish
open Elmish.WPF.Internal

let private run
    (config: ElmConfig)
    (window: Window)
    (programRun: Program<'t, 'model, 'msg, BindingSpec<'model, 'msg> list> -> unit)
    (program: Program<'t, 'model, 'msg, BindingSpec<'model, 'msg> list>) =
  let mutable lastModel = None

  let setState model dispatch =
    match lastModel with
    | None ->
        let mapping = program.view model dispatch
        let vm = ViewModel<'model,'msg>(model, dispatch, mapping, config)
        window.DataContext <- vm
        lastModel <- Some vm
    | Some vm ->
        vm.UpdateModel model

  // Start Elmish dispatch loop
  programRun { program with setState = setState }

  // Start WPF dispatch loop
  let app = if isNull Application.Current then Application() else Application.Current
  app.Run window

/// Starts both Elmish and WPF dispatch loops. Blocking function.
let runWindow window program =
  run ElmConfig.Default window Elmish.Program.run program

/// Starts both Elmish and WPF dispatch loops with the specified configuration.
/// Blocking function.
let runWindowWithConfig config window program =
  run config window Elmish.Program.run program
