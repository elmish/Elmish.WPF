module Elmish.WPF.Samples.Validation.Program

open System
open System.Linq
open Serilog
open Serilog.Extensions.Logging
open Elmish
open Elmish.WPF


let requireNotEmpty s =
  if String.IsNullOrEmpty s then Error "This field is required" else Ok s

let parseInt (s: string) =
  match Int32.TryParse s with
  | true, i -> Ok i
  | false, _ -> Error "Please enter a valid integer"

let requireExactly y x =
  if x = y then Ok x else Error <| sprintf "Please enter %A" y

let validateInt42 =
  requireNotEmpty
  >> Result.bind parseInt
  >> Result.bind (requireExactly 42)


let validatePassword (s: string) =
  [
    if s.All(fun c -> Char.IsDigit c |> not) then
      "Must contain a digit"
    if s.All(fun c -> Char.IsLower c |> not) then
      "Must contain a lowercase letter"
    if s.All(fun c -> Char.IsUpper c |> not) then
      "Must contain an uppercase letter"
  ]


type Model =
  { Value: string
    Password: string }

let init () =
  { Value = ""
    Password = "" }

type Msg =
  | NewValue of string
  | NewPassword of string
  | Submit

let update msg m =
  match msg with
  | NewValue x -> { m with Value = x }
  | NewPassword x -> { m with Password = x }
  | Submit -> m

let bindings () : Binding<Model, Msg> list = [
  "Value" |> Binding.twoWayValidate(
    (fun m -> m.Value),
    NewValue,
    (fun m ->  validateInt42 m.Value))
  "Password" |> Binding.twoWayValidate(
    (fun m -> m.Password),
    NewPassword,
    (fun m -> validatePassword m.Password))
  "Submit" |> Binding.cmdIf(
    (fun _ -> Submit),
    (fun m -> (match validateInt42 m.Value with Ok _ -> true | Error _ -> false) && (validatePassword m.Password |> List.isEmpty)))
]

let designVm = ViewModel.designInstance (init ()) (bindings ())

let main window =
  let logger =
    LoggerConfiguration()
      .MinimumLevel.Override("Elmish.WPF.Update", Events.LogEventLevel.Verbose)
      .MinimumLevel.Override("Elmish.WPF.Bindings", Events.LogEventLevel.Verbose)
      .MinimumLevel.Override("Elmish.WPF.Performance", Events.LogEventLevel.Verbose)
      .WriteTo.Console()
      .CreateLogger()

  WpfProgram.mkSimple init update bindings
  |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))
  |> WpfProgram.startElmishLoop window
