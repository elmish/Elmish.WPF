[<RequireQualifiedAccess>]
module Elmish.WPF.Cmd

open System
open System.Windows
open Elmish

/// Returns a command to display the window returned by the specified
/// function. The function can for example be a simple window constructor,
/// or a function that instantiates and configures the window (setting owner etc.)
/// The window's DataContext will be set to the same as the main window.
[<Obsolete("Use Binding.subModelWin to control windows and dialogs")>]
let showWindow (getWindow: unit -> #Window) =
  let showWin () =
    Application.Current.Dispatcher.Invoke(fun () ->
      let win = getWindow ()
      win.DataContext <- Application.Current.MainWindow.DataContext
      win.Show()
    )
  Cmd.OfFunc.attempt showWin () raise
