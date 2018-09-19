[<RequireQualifiedAccess>]
module Elmish.WPF.Program

open System.Windows
open Elmish.WPF.Utilities

/// Start WPF dispatch loop. Blocking function.
let private startApp window =
  let app = if isNull Application.Current then Application() else Application.Current
  app.Run window

/// Starts both Elmish and WPF dispatch loops. Blocking function.
let runWindow window program =
  ViewModel.startLoop ElmConfig.Default window Elmish.Program.run program
  startApp window

/// Starts both Elmish and WPF dispatch loops with the specified configuration.
/// Blocking function.
let runWindowWithConfig config window program =
  ViewModel.startLoop config window Elmish.Program.run program
  startApp window
