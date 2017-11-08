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
          Postal = "N1N 1N1"
          IsDialogVisible = false
          DialogResult = None }

    let update (msg:Msg) (model:Model) =
        match msg with
        | SetName str -> { model with Name = str }
        | SetEmail str -> { model with Email = str }
        | SetAge a -> { model with Age = int a }
        | SetAddress str -> { model with Address = str }
        | SetPhone str -> { model with Phone = str }
        | SetPostal str -> { model with Postal = str }
        | SetDialogVisible v -> { model with IsDialogVisible = v }
        | SetDialogResult r -> { model with DialogResult = r; IsDialogVisible = false }
        

module App =
    open State

    let view _ _ =
        [ "Name" |> Binding.twoWay (fun m -> m.Name) (fun v m -> SetName v)
          "Email" |> Binding.twoWay (fun m -> m.Email) (fun v m -> SetEmail v)
          "Age" |> Binding.twoWay (fun m -> string m.Age) (fun v m -> SetAge v)
          "Address" |> Binding.twoWay (fun m -> m.Address) (fun v m -> SetAddress v)
          "Phone" |> Binding.twoWay (fun m -> m.Phone) (fun v m -> SetPhone v)
          "Postal" |> Binding.twoWay (fun m -> m.Postal) (fun v m -> SetPostal v)

          "IsEditable" |> Binding.oneWay (fun m -> not m.IsDialogVisible)
          "IsDialogVisible" |> Binding.oneWay (fun m -> m.IsDialogVisible)
          "HasDialogResult" |> Binding.oneWay (fun m -> m.DialogResult.IsSome)
          "CurrentDialogResult" |> Binding.oneWay (fun m -> string m.DialogResult)
          "ShowDialog" |> Binding.cmd (fun _ m -> SetDialogVisible true)
          "Ok" |> Binding.cmd (fun _ m -> SetDialogResult <| Some true)
          "Cancel" |> Binding.cmd (fun _ m -> SetDialogResult <| Some false)
          "Clear" |> Binding.cmd (fun _ m -> SetDialogResult None) ]

    [<EntryPoint;STAThread>]
    let main argv = 
        Program.mkSimple init update view
//        |> Program.withConsoleTrace
//        |> Program.withSubscription subscribe
        |> Program.runWindow (MainWindow())