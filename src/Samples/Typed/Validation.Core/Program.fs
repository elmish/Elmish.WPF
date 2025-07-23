module Elmish.WPF.Samples.Validation.Program

open System
open System.Linq
open Serilog
open Serilog.Extensions.Logging
open Elmish.WPF

module Result =
    module Error =
        let toList =
            function
            | Ok _ -> []
            | Error e -> [ e ]

let requireNotEmpty s =
    if String.IsNullOrEmpty s then
        Error "This field is required"
    else
        Ok s

let parseInt (s: string) =
    match Int32.TryParse s with
    | true, i -> Ok i
    | false, _ -> Error "Please enter a valid integer"

let requireExactly y x =
    if x = y then Ok x else Error <| sprintf "Please enter %A" y

let validateInt42 =
    requireNotEmpty >> Result.bind parseInt >> Result.bind (requireExactly 42)

let validatePassword (s: string) =
    [ if s.All(fun c -> Char.IsDigit c |> not) then
          "Must contain a digit"
      if s.All(fun c -> Char.IsLower c |> not) then
          "Must contain a lowercase letter"
      if s.All(fun c -> Char.IsUpper c |> not) then
          "Must contain an uppercase letter" ]

module Validation =
    type Model =
        { UpdateCount: int
          Value: string
          Password: string }

    let init () =
        { UpdateCount = 0
          Value = ""
          Password = "" }

    type Msg =
        | NewValue of string
        | NewPassword of string
        | Submit

    let increaseUpdateCount m =
        { m with
            UpdateCount = m.UpdateCount + 1 }

    let update msg m =
        let m = increaseUpdateCount m

        match msg with
        | NewValue x -> { m with Value = x }
        | NewPassword x -> { m with Password = x }
        | Submit -> m

    let errorOnEven m =
        if m.UpdateCount % 2 = 0 then
            [ "Even counts have this error" ]
        else
            []

    let canSubmit m =
        (match validateInt42 m.Value with
         | Ok _ -> true
         | Error _ -> false)
        && (validatePassword m.Password |> List.isEmpty)

[<AllowNullLiteral>]
type ValidationViewModel(args) =
    inherit ViewModelBase<Validation.Model, Validation.Msg>(args)

    let valueBinding =
        Binding.TwoWayT.id
        >> Binding.addLazy (=)
        >> Binding.mapModel (fun (m: Validation.Model) -> m.Value)
        >> Binding.mapMsg Validation.NewValue
        >> Binding.addValidation (fun m -> m.Value |> validateInt42 |> Result.Error.toList)

    let passwordBinding =
        Binding.TwoWayT.id
        >> Binding.addLazy (=)
        >> Binding.mapModel (fun (m: Validation.Model) -> m.Password)
        >> Binding.mapMsg Validation.NewPassword
        >> Binding.addValidation (fun m -> m.Password |> validatePassword)

    member _.UpdateCount =
        base.Get
            ()
            (Binding.OneWayT.id
             >> Binding.addLazy (=)
             >> Binding.mapModel (fun (m: Validation.Model) -> m.UpdateCount)
             >> Binding.addValidation Validation.errorOnEven)

    member this.Value
        with get () = base.Get () valueBinding
        and set (value) = base.Set (value) valueBinding

    member this.Password
        with get () = base.Get () passwordBinding
        and set (value) = base.Set (value) passwordBinding

    member _.Submit = base.Get () (Binding.CmdT.set Validation.canSubmit Validation.Submit)

let main window =
    let logger =
        LoggerConfiguration()
            .MinimumLevel.Override("Elmish.WPF.Update", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Bindings", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Performance", Events.LogEventLevel.Verbose)
            .WriteTo.Console()
            .CreateLogger()

    let createVm args = ValidationViewModel(args)

    WpfProgram.mkSimpleT Validation.init Validation.update createVm
    |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))
    |> WpfProgram.startElmishLoop window