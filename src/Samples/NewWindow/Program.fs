module Elmish.WPF.Samples.NewWindow.Program

open System
open System.Windows
open Elmish
open Elmish.WPF

module App =

  type ConfirmState =
    | SubmitClicked
    | CancelClicked
    | CloseRequested

  type Win2 = {
    Input: string
    IsChecked: bool
    ConfirmState: ConfirmState option
  }

  type Model =
    { Win1State: WindowState<string>
      Win1Input: string
      Win2: Win2 option }

  let init () =
    { Win1State = WindowState.Closed
      Win1Input = ""
      Win2 = None }

  let initWindow2 =
    { Input = ""
      IsChecked = false
      ConfirmState = None }

  type Msg =
    | ShowWin1
    | HideWin1
    | CloseWin1
    | ShowWin2
    | Win1Input of string
    | Win2Input of string
    | Win2SetChecked of bool
    | Win2Submit
    | Win2ButtonCancel
    | Win2CloseRequested

  let update msg m =
    match msg with
    | ShowWin1 -> { m with Win1State = WindowState.Visible "" }
    | HideWin1 -> { m with Win1State = WindowState.Hidden "" }
    | CloseWin1 -> { m with Win1State = WindowState.Closed }
    | ShowWin2 -> { m with Win2 = Some initWindow2 }
    | Win1Input s -> { m with Win1Input = s }
    | Win2Input s ->
        { m with
            Win2 =
              m.Win2
              |> Option.map (fun m' -> { m' with Input = s })
          }
    | Win2SetChecked isChecked ->
        { m with
            Win2 =
              m.Win2
              |> Option.map (fun m' -> { m' with IsChecked = isChecked })
            }
    | Win2Submit ->
        match m.Win2 with
        | Some { ConfirmState = Some SubmitClicked } -> { m with Win2 = None }
        | Some win2 -> { m with Win2 = Some { win2 with ConfirmState = Some SubmitClicked } }
        | None -> m
    | Win2ButtonCancel ->
        match m.Win2 with
        | Some { ConfirmState = Some CancelClicked } -> { m with Win2 = None }
        | Some win2 -> { m with Win2 = Some { win2 with ConfirmState = Some CancelClicked } }
        | None -> m
    | Win2CloseRequested -> 
        match m.Win2 with
        | Some { ConfirmState = Some CloseRequested } -> { m with Win2 = None }
        | Some win2 -> { m with Win2 = Some { win2 with ConfirmState = Some CloseRequested } }
        | None -> m

  let window1Bindings () = [
    "Input" |> Binding.twoWay((fun m -> m.Win1Input), Win1Input)
  ]

  let window2Bindings () = [
    "Input" |> Binding.twoWay((fun m -> m.Input), Win2Input)
    "IsChecked" |> Binding.twoWay((fun m -> m.IsChecked), Win2SetChecked)
    "Submit" |> Binding.cmd Win2Submit
    "Cancel" |> Binding.cmd Win2ButtonCancel
    "SubmitMsgVisible" |> Binding.oneWay (fun m -> m.ConfirmState = Some SubmitClicked)
    "CancelMsgVisible" |> Binding.oneWay (fun m -> m.ConfirmState = Some CancelClicked)
    "CloseRequestedMsgVisible" |> Binding.oneWay (fun m -> m.ConfirmState = Some CloseRequested)
  ]

  let mainBindings () : Binding<Model, Msg> list = [
    "ShowWin1" |> Binding.cmd ShowWin1
    "HideWin1" |> Binding.cmd HideWin1
    "CloseWin1" |> Binding.cmd CloseWin1
    "ShowWin2" |> Binding.cmd ShowWin2
    "Win1" |> Binding.subModelWin(
      (fun m -> m.Win1State), fst, id,
      window1Bindings,
      Window1)
    "Win2" |> Binding.subModelWin(
      (fun m -> m.Win2 |> WindowState.ofOption), snd, id,
      window2Bindings,
      (fun () -> Window2(Owner = Application.Current.MainWindow)),
      onCloseRequested = Win2CloseRequested,
      isModal = true)
  ]


[<EntryPoint; STAThread>]
let main _ =
  Program.mkSimpleWpf App.init App.update App.mainBindings
  |> Program.withConsoleTrace
  |> Program.runWindowWithConfig
    { ElmConfig.Default with LogConsole = true; Measure = true }
    (MainWindow())
