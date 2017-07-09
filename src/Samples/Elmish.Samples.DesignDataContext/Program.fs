namespace Elmish.Samples.DesignDataContext

open System
open Elmish
open Elmish.WPF
        
module State =
    let init() = 
        { Name = "Frank"
          Email = "frank@rabbit.com"
          Age = 28
          Address = "1 Anderson Lane"
          Phone = "64212"
          Postal = "N1N 1N1" }

    let update (msg:Msg) (model:Model) =
        match msg with
        | SetName str -> { model with Name = str }
        | SetEmail str -> { model with Email = str }
        | SetAge a -> { model with Age = a }
        | SetAddress str -> { model with Address = str }
        | SetPhone str -> { model with Phone = str }
        | SetPostal str -> { model with Postal = str }
        

module App =
    open State

    let view _ _ =
        [ "Name" |> Binding.twoWay (fun m -> m.Name) (fun v m -> SetName v)
          "Email" |> Binding.twoWay (fun m -> m.Email) (fun v m -> SetEmail v)
          "Age" |> Binding.twoWay (fun m -> m.Age) (fun v m -> SetAge v)
          "Address" |> Binding.twoWay (fun m -> m.Address) (fun v m -> SetAddress v)
          "Phone" |> Binding.twoWay (fun m -> m.Phone) (fun v m -> SetPhone v)
          "Postal" |> Binding.twoWay (fun m -> m.Postal) (fun v m -> SetPostal v) ]

    [<EntryPoint;STAThread>]
    let main argv = 
        Program.mkSimple init update view
//        |> Program.withConsoleTrace
//        |> Program.withSubscription subscribe
        |> Program.runWindow (MainWindow())