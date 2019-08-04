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
    Input1: string
    Input2: string
    ConfirmState: ConfirmState option
  }

  type Model =
    { Win1State: WindowState<string>
      Win1Input: string
      Win2: Win2 option }

  let init () =
    { Win1State = WindowState.Closed
      Win1Input = ""
      Win2 = None },
    Cmd.none

  type Msg =
    | ShowWin1
    | HideWin1
    | CloseWin1
    | ShowWin2
    | Win1Input of string
    | Win2Input1 of string
    | Win2Input2 of string
    | Win2Submit
    | Win2ButtonCancel
    | Win2CloseRequested

  let update msg m =
    match msg with
    | ShowWin1 -> { m with Win1State = WindowState.Visible "" }, Cmd.none
    | HideWin1 -> { m with Win1State = WindowState.Hidden "" }, Cmd.none
    | CloseWin1 -> { m with Win1State = WindowState.Closed }, Cmd.none
    | ShowWin2 ->
        let win2 = { Input1 = ""; Input2 = ""; ConfirmState = None }
        { m with Win2 = Some win2 }, Cmd.none
    | Win1Input s -> { m with Win1Input = s }, Cmd.none
    | Win2Input1 s ->
        { m with
            Win2 =
              m.Win2
              |> Option.map (fun m' -> { m' with Input1 = s })
          },
        Cmd.none
    | Win2Input2 s ->
        { m with
            Win2 =
              m.Win2
              |> Option.map (fun m' -> { m' with Input2 = s })
            },
        Cmd.none
    | Win2Submit ->
        let newM =
          match m.Win2 with
          | Some { ConfirmState = Some SubmitClicked } -> { m with Win2 = None }
          | Some win2 -> { m with Win2 = Some { win2 with ConfirmState = Some SubmitClicked } }
          | None -> m
        newM, Cmd.none
    | Win2ButtonCancel ->
        let newM =
          match m.Win2 with
          | Some { ConfirmState = Some CancelClicked } -> { m with Win2 = None }
          | Some win2 -> { m with Win2 = Some { win2 with ConfirmState = Some CancelClicked } }
          | None -> m
        newM, Cmd.none
    | Win2CloseRequested -> 
        let newM =
          match m.Win2 with
          | Some { ConfirmState = Some CloseRequested } -> { m with Win2 = None }
          | Some win2 -> { m with Win2 = Some { win2 with ConfirmState = Some CloseRequested } }
          | None -> m
        newM, Cmd.none


  let bindings () : Binding<Model, Msg> list = [
    "ShowWin1" |> Binding.cmd ShowWin1
    "HideWin1" |> Binding.cmd HideWin1
    "CloseWin1" |> Binding.cmd CloseWin1
    "ShowWin2" |> Binding.cmd ShowWin2
    "Win1" |> Binding.subModelWin(
      (fun m -> m.Win1State), fst, id,
      (fun () -> [
        "Input" |> Binding.twoWay((fun m -> m.Win1Input), Win1Input)
      ]),
      Window1
    )
    "Win2" |> Binding.subModelWin(
      (fun m -> m.Win2 |> WindowState.ofOption), snd, id,
      (fun () -> [
        "Input1" |> Binding.twoWay((fun m -> m.Input1), Win2Input1)
        "Input2" |> Binding.twoWay((fun m -> m.Input2), Win2Input2)
        "Submit" |> Binding.cmd Win2Submit
        "Cancel" |> Binding.cmd Win2ButtonCancel
        "SubmitMsgVisible" |> Binding.oneWay (fun m -> m.ConfirmState = Some SubmitClicked)
        "CancelMsgVisible" |> Binding.oneWay (fun m -> m.ConfirmState = Some CancelClicked)
        "CloseRequestedMsgVisible" |> Binding.oneWay (fun m -> m.ConfirmState = Some CloseRequested)
      ]),
      (fun () -> Window2(Owner = Application.Current.MainWindow)),
      onCloseRequested = Win2CloseRequested,
      isModal = true
    )
  ]


[<EntryPoint; STAThread>]
let main argv =
  Program.mkProgramWpf App.init App.update App.bindings
  |> Program.withConsoleTrace
  |> Program.runWindowWithConfig
      { ElmConfig.Default with LogConsole = true; Measure = true }
      (MainWindow())
