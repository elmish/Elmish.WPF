module Elmish.WPF.Samples.Validation.Program

open System
open Elmish
open Elmish.WPF


let requireNotEmpty s =
  if String.IsNullOrEmpty s then Error "This field is required" else Ok s

let parseInt s =
  match Int32.TryParse s with
  | true, i -> Ok i
  | false, _ -> Error "Please enter a valid integer"

let requireExactly y x =
  if x = y then Ok x else Error <| sprintf "Please enter %A" y

let validateInt42 =
  requireNotEmpty
  >> Result.bind parseInt
  >> Result.bind (requireExactly 42)


type Model =
  { Field1Int: int
    Field2Raw: string }

let init () =
  { Field1Int = 0
    Field2Raw = "" }

type Msg =
  | Field1Input of int
  | Field2Input of string
  | Submit of int

let update msg m =
  match msg with
  | Field1Input x -> { m with Field1Int = x }
  | Field2Input x -> { m with Field2Raw = x }
  | Submit x -> m

let bindings model dispatch =
  [
    "Field1" |> Binding.twoWayIfValid
      (fun m -> string m.Field1Int)
      (fun v m ->
        unbox v |> validateInt42 |> Result.map Field1Input)
    "Field2" |> Binding.twoWayValidate
      (fun m -> m.Field2Raw)
      (fun v m -> Field2Input v)
      (fun m ->  validateInt42 m.Field2Raw)
    "Submit" |> Binding.cmdIfValid
      (fun m -> validateInt42 m.Field2Raw |> Result.map Submit)
  ]


[<EntryPoint; STAThread>]
let main argv =
  Program.mkSimple init update bindings
  |> Program.withConsoleTrace
  |> Program.runWindowWithConfig
      { ElmConfig.Default with LogConsole = true }
      (MainWindow())
