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
          Postal = "N1N1N1"
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
    
    let isValidEmail str = 
        let emailRegex = @"\A(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)\Z"
        System.Text.RegularExpressions.Regex.IsMatch(str, emailRegex, System.Text.RegularExpressions.RegexOptions.IgnoreCase)

    let isInt str = Int32.TryParse str |> fst

    let view _ _ =
        [ "Name" |> Binding.twoWayValidation (fun m -> m.Name) (fun v m -> if v.Length > 2 then SetName v |> Ok else Error "Name is invalid")
          "Email" |> Binding.twoWayValidation (fun m -> m.Email) (fun v m -> if isValidEmail v then SetEmail v |> Ok else Error "Email is invalid")
          "Age" |> Binding.twoWayValidation (fun m -> string m.Age) (fun v m -> if isInt v then SetAge v |> Ok else Error "Age is invalid")
          "Address" |> Binding.twoWay (fun m -> m.Address) (fun v m -> SetAddress v)
          "Phone" |> Binding.twoWay (fun m -> m.Phone) (fun v m -> SetPhone v)
          "Postal" |> Binding.twoWayValidation (fun m -> m.Postal) (fun v m -> if v.Length > 6 then Error "Postal code is invalid" else SetPostal v |> Ok)

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
        |> Program.withMessageBoxErrorHandler
//        |> Program.withConsoleTrace
//        |> Program.withSubscription subscribe
        |> Program.runWindow (MainWindow())