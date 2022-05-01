module Elmish.WPF.Samples.Validation.Program

open System
open System.Linq
open Serilog
open Serilog.Extensions.Logging
open Elmish.WPF


module Result =

  module Error =

    let toList = function
      | Ok _ -> []
      | Error e -> [ e ]


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
  { m with UpdateCount = m.UpdateCount + 1 }

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

let bindings () : Binding<Model, Msg, obj> list = [
  "UpdateCount"
    |> Binding.oneWay(fun m -> m.UpdateCount)
    |> Binding.addValidation errorOnEven
  "Value"
    |> Binding.twoWay((fun m -> m.Value), NewValue)
    |> Binding.addValidation(fun m ->  m.Value |> validateInt42 |> Result.Error.toList)
  "Password"
    |> Binding.twoWay((fun m -> m.Password), NewPassword)
    |> Binding.addValidation(fun m -> m.Password |> validatePassword)
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
