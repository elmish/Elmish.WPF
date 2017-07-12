[<RequireQualifiedAccess>]
module Elmish.WPF.Program

open System.Windows
open Elmish.WPF.ViewModel
open Elmish

let withExceptionHandler f program =
    { program with onError = f }

/// Blocking function
/// Starts both Elmish and WPF dispatch loops.
let runWindow (window:Window) (program: Program<unit, 'model, 'msg, ViewBindings<'model,'msg>>) = 

    let mutable lastModel = None

    let setState model dispatch = 
        match lastModel with
        | None -> 
            let mapping = program.view model dispatch
            let vm = ViewModelBase<'model,'msg>(model, dispatch, mapping)
            window.DataContext <- vm
            lastModel <- Some vm
        | Some vm ->
            vm.UpdateModel model
                  
    // Start Elmish dispatch loop  
    { program with setState = setState } 
    |> Elmish.Program.run
    
    // Start WPF dispatch loop
    let app = Application()
    app.Run(window) //blocking