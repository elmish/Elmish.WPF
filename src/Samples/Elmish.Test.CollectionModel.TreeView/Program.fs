
open System
open Elmish
open Elmish.WPF
open TreeViewModel
open Model

[<EntryPoint;STAThread>]
let main argv = 
    Program.mkProgram init update view
    |> Program.withConsoleTrace
    |> Program.runWindow (MainWindow())
