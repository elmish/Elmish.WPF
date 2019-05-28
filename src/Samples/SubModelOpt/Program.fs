module Elmish.WPF.Samples.SubModelOpt.Program

open System
open Elmish
open Elmish.WPF


module Form1 =

  type Model =
    { Text: string }

  type Msg =
    | TextInput of string
    | Submit

  let init =
    { Text = "" }

  let update msg m =
    match msg with
    | TextInput s -> { m with Text = s }
    | Submit -> m  // handled by parent

  let bindings () : Binding<Model, Msg> list = [
    "Text" |> Binding.twoWay ((fun m -> m.Text), TextInput)
    "Submit" |> Binding.cmd Submit
  ]


module Form2 =

  type Model =
    { Input1: string
      Input2: string }

  type Msg =
    | Text1Input of string
    | Text2Input of string
    | Submit

  let init =
    { Input1 = ""
      Input2 = "" }

  let update msg m =
    match msg with
    | Text1Input s -> { m with Input1 = s }
    | Text2Input s -> { m with Input2 = s }
    | Submit -> m  // handled by parent

  let bindings () : Binding<Model, Msg> list = [
    "Input1" |> Binding.twoWay ((fun m -> m.Input1), Text1Input)
    "Input2" |> Binding.twoWay ((fun m -> m.Input2), Text2Input)
    "Submit" |> Binding.cmd Submit
  ]


module App =

  type Dialog =
    | Form1 of Form1.Model
    | Form2 of Form2.Model

  type Model =
    { Dialog: Dialog option }

  let init () =
    { Dialog = None }

  type Msg =
    | ShowForm1
    | ShowForm2
    | Form1Msg of Form1.Msg
    | Form2Msg of Form2.Msg

  let update msg m =
    match msg with
    | ShowForm1 -> { m with Dialog = Some <| Form1 Form1.init }
    | ShowForm2 -> { m with Dialog = Some <| Form2 Form2.init }
    | Form1Msg Form1.Submit -> { m with Dialog = None }
    | Form1Msg msg' ->
        match m.Dialog with
        | Some (Form1 m') -> { m with Dialog = Form1.update msg' m' |> Form1 |> Some }
        | _ -> m
    | Form2Msg Form2.Submit -> { m with Dialog = None }
    | Form2Msg msg' ->
        match m.Dialog with
        | Some (Form2 m') -> { m with Dialog = Form2.update msg' m' |> Form2 |> Some }
        | _ -> m

  let bindings () : Binding<Model, Msg> list = [
    "ShowForm1" |> Binding.cmd ShowForm1

    "ShowForm2" |> Binding.cmd ShowForm2

    "DialogVisible" |> Binding.oneWay (fun m -> m.Dialog.IsSome)

    "Form1Visible" |> Binding.oneWay
      (fun m -> match m.Dialog with Some (Form1 _) -> true | _ -> false)

    "Form2Visible" |> Binding.oneWay
      (fun m -> match m.Dialog with Some (Form2 _) -> true | _ -> false)

    "Form1" |> Binding.subModelOpt(
      (fun m -> match m.Dialog with Some (Form1 m') -> Some m' | _ -> None),
      snd,
      Form1Msg,
      Form1.bindings)

    "Form2" |> Binding.subModelOpt(
      (fun m -> match m.Dialog with Some (Form2 m') -> Some m' | _ -> None),
      snd,
      Form2Msg,
      Form2.bindings)
  ]


[<EntryPoint; STAThread>]
let main argv =
  Program.mkSimpleWpf App.init App.update App.bindings
  |> Program.withConsoleTrace
  |> Program.runWindowWithConfig
      { ElmConfig.Default with LogConsole = true; Measure = true }
      (MainWindow())
