[<RequireQualifiedAccess>]
module Elmish.WPF.Program

open System.Windows
open Elmish.WPF.ViewModel
open Elmish

let withMessageBoxErrorHandler program =
    program 
    |> Program.withErrorHandler (fun (_, ex) -> System.Windows.MessageBox.Show(ex.Message) |> ignore)

let private _run debug (window:Window) (programRun:Program<'t, 'model, 'msg, ViewBindings<'model,'msg>> -> unit) (program: Program<'t, 'model, 'msg, ViewBindings<'model,'msg>>) =
    let mutable lastModel = None

    let setState model dispatch = 
        match lastModel with
        | None -> 
            let mapping = program.view model dispatch
            let vm = ViewModelBase<'model,'msg>(model, dispatch, mapping, debug)
            window.DataContext <- vm
            lastModel <- Some vm
        | Some vm ->
            vm.UpdateModel model
                  
    // Start Elmish dispatch loop  
    { program with setState = setState } 
    |> programRun
    
    // Start WPF dispatch loop
    let app = Application()
    app.Run(window) //blocking

/// Blocking function.
/// Starts both Elmish and WPF dispatch loops.
let runWindow window program = _run false window Elmish.Program.run program

let runWindowWith<'t, 'model, 'msg> window (initialValue:'t) (program: Program<'t, 'model, 'msg, ViewBindings<'model,'msg>>)  =
    _run false window (Elmish.Program.runWith initialValue) program

/// Blocking function.
/// Starts both Elmish and WPF dispatch loops.
/// Enables debug console logging.
let runDebugWindow window program = _run true window  Elmish.Program.run program