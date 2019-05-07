module Elmish.WPF.Samples.NewWindow.Program

open System
open System.Windows
open Elmish
open Elmish.WPF


module Win1 =

  type Model =
    { Text: string }

  type Msg =
    | TextInput of string

  let init =
    { Text = "" }

  let update msg m =
    match msg with
    | TextInput s -> { m with Text = s }

  let bindings () : Binding<Model, Msg> list = [
    "Text" |> Binding.twoWay (fun m -> m.Text) (fun v m -> TextInput v)
  ]


module Win2 =

  type Model =
    { Input1: string
      Input2: string }

  type Msg =
    | Text1Input of string
    | Text2Input of string

  let init =
    { Input1 = ""
      Input2 = "" }

  let update msg m =
    match msg with
    | Text1Input s -> { m with Input1 = s }
    | Text2Input s -> { m with Input2 = s }

  let bindings () : Binding<Model, Msg> list = [
    "Input1" |> Binding.twoWay (fun m -> m.Input1) (fun v m -> Text1Input v)
    "Input2" |> Binding.twoWay (fun m -> m.Input2) (fun v m -> Text2Input v)
  ]


module App =

  type Model =
    { Win1: Win1.Model
      Win2: Win2.Model }

  let init () =
    { Win1 = Win1.init
      Win2 = Win2.init },
    Cmd.none

  type Msg =
    | ShowWin1
    | ShowWin2
    | Win1Msg of Win1.Msg
    | Win2Msg of Win2.Msg

  let showWin1 () =
    Application.Current.Dispatcher.Invoke(fun () ->
      let win1 = Window1()
      win1.DataContext <- Application.Current.MainWindow.DataContext
      win1.Show()
    )

  let showWin2 () =
    Application.Current.Dispatcher.Invoke(fun () ->
      let win2 = Window2()
      win2.DataContext <- Application.Current.MainWindow.DataContext
      win2.Show()
    )


  let update msg m =
    match msg with
    | ShowWin1 -> m, Cmd.OfFunc.attempt showWin1 () raise
    | ShowWin2 -> m, Cmd.OfFunc.attempt showWin2 () raise
    | Win1Msg msg' -> { m with Win1 = Win1.update msg' m.Win1 }, Cmd.none
    | Win2Msg msg' -> { m with Win2 = Win2.update msg' m.Win2 }, Cmd.none


  let bindings () : Binding<Model, Msg> list = [
    "ShowWin1" |> Binding.cmd (fun m -> ShowWin1)

    "ShowWin2" |> Binding.cmd (fun m -> ShowWin2)

    "Win1" |> Binding.subModel
      (fun m -> m.Win1)
      snd
      Win1Msg
      Win1.bindings

    "Win2" |> Binding.subModel
      (fun m -> m.Win2)
      snd
      Win2Msg
      Win2.bindings
  ]


[<EntryPoint; STAThread>]
let main argv =
  Program.mkProgram App.init App.update (fun _ _ -> App.bindings ())
  |> Program.withConsoleTrace
  |> Program.runWindowWithConfig
      { ElmConfig.Default with LogConsole = true }
      (MainWindow())
