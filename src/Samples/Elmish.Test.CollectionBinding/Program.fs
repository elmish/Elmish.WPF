open System
open Elmish
open Elmish.WPF
open TwoWayList
open App

[<EntryPoint;STAThread>]
let main argv = 
    Program.mkProgram init update view
    |> Program.withConsoleTrace
    |> Program.runWindow (MainWindow())
