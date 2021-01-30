module Elmish.WPF.Samples.FileDialogsCmdMsg.Program

open System
open Elmish
open Elmish.WPF


module Core =


  type Model =
    { CurrentTime: DateTimeOffset
      Text: string
      StatusMsg: string }


  type CmdMsg =
    | Save of string
    | Load


  let init () =
    { CurrentTime = DateTimeOffset.Now
      Text = ""
      StatusMsg = "" },
    []

  type Msg =
    | SetTime of DateTimeOffset
    | SetText of string
    | RequestSave
    | RequestLoad
    | SaveSuccess
    | LoadSuccess of string
    | SaveCanceled
    | LoadCanceled
    | SaveFailed of exn
    | LoadFailed of exn


  let update msg m =
    match msg with
    | SetTime t -> { m with CurrentTime = t }, []
    | SetText s -> { m with Text = s}, []
    | RequestSave -> m, [Save m.Text]
    | RequestLoad -> m, [Load]
    | SaveSuccess -> { m with StatusMsg = sprintf "Successfully saved at %O" DateTimeOffset.Now }, []
    | LoadSuccess s -> { m with Text = s; StatusMsg = sprintf "Successfully loaded at %O" DateTimeOffset.Now }, []
    | SaveCanceled -> { m with StatusMsg = "Saving canceled" }, []
    | LoadCanceled -> { m with StatusMsg = "Loading canceled" }, []
    | SaveFailed ex -> { m with StatusMsg = sprintf "Saving failed with excption %s: %s" (ex.GetType().Name) ex.Message }, []
    | LoadFailed ex -> { m with StatusMsg = sprintf "Loading failed with excption %s: %s" (ex.GetType().Name) ex.Message }, []



module Platform =

  open System.IO
  open System.Threading
  open System.Windows
  open Core


  let bindings () : Binding<Model, Msg> list = [
    "CurrentTime" |> Binding.oneWay (fun m -> m.CurrentTime)
    "Text" |> Binding.twoWay ((fun m -> m.Text), SetText)
    "StatusMsg" |> Binding.twoWay ((fun m -> m.StatusMsg), SetText)
    "Save" |> Binding.cmd RequestSave
    "Load" |> Binding.cmd RequestLoad
  ]


  let save text =
    Application.Current.Dispatcher.Invoke(fun () ->
      let guiCtx = SynchronizationContext.Current
      async {
        do! Async.SwitchToContext guiCtx
        let dlg = Microsoft.Win32.SaveFileDialog ()
        dlg.Filter <- "Text file (*.txt)|*.txt|Markdown file (*.md)|*.md"
        let result = dlg.ShowDialog ()
        if result.HasValue && result.Value then
          do! File.WriteAllTextAsync(dlg.FileName, text) |> Async.AwaitTask
          return SaveSuccess
        else return SaveCanceled
      }
    )


  let load () =
    Application.Current.Dispatcher.Invoke(fun () ->
      let guiCtx = SynchronizationContext.Current
      async {
        do! Async.SwitchToContext guiCtx
        let dlg = Microsoft.Win32.OpenFileDialog ()
        dlg.Filter <- "Text file (*.txt)|*.txt|Markdown file (*.md)|*.md"
        dlg.DefaultExt <- "txt"
        let result = dlg.ShowDialog ()
        if result.HasValue && result.Value then
          let! contents = File.ReadAllTextAsync(dlg.FileName) |> Async.AwaitTask
          return LoadSuccess contents
        else return LoadCanceled
      }
    )


  let toCmd = function
    | Save text -> Cmd.OfAsync.either save text id SaveFailed
    | Load -> Cmd.OfAsync.either load () id LoadFailed



open Core
open Platform


let designVm = ViewModel.designInstance (init () |> fst) (bindings ())


let timerTick dispatch =
  let timer = new Timers.Timer(1000.)
  timer.Elapsed.Add (fun _ -> dispatch (SetTime DateTimeOffset.Now))
  timer.Start()


let main window =
  Program.mkProgramWpfWithCmdMsg init update bindings toCmd
  |> Program.withSubscription (fun _ -> Cmd.ofSub timerTick)
  |> Program.withConsoleTrace
  |> Program.startElmishLoop
    { ElmConfig.Default with LogConsole = true; Measure = true }
    window
