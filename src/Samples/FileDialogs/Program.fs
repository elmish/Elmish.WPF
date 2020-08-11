module Elmish.WPF.Samples.FileDialogs.Program

open System
open System.IO
open System.Threading
open System.Windows
open Serilog
open Serilog.Extensions.Logging
open Elmish
open Elmish.WPF


type Model =
  { CurrentTime: DateTimeOffset
    Text: string
    StatusMsg: string }
    
        
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


let update msg m =
  match msg with
  | SetTime t -> { m with CurrentTime = t }, Cmd.none
  | SetText s -> { m with Text = s}, Cmd.none
  | RequestSave -> m, Cmd.OfAsync.either save m.Text id SaveFailed
  | RequestLoad -> m, Cmd.OfAsync.either load () id LoadFailed
  | SaveSuccess -> { m with StatusMsg = sprintf "Successfully saved at %O" DateTimeOffset.Now }, Cmd.none
  | LoadSuccess s -> { m with Text = s; StatusMsg = sprintf "Successfully loaded at %O" DateTimeOffset.Now }, Cmd.none
  | SaveCanceled -> { m with StatusMsg = "Saving canceled" }, Cmd.none
  | LoadCanceled -> { m with StatusMsg = "Loading canceled" }, Cmd.none
  | SaveFailed ex -> { m with StatusMsg = sprintf "Saving failed with excption %s: %s" (ex.GetType().Name) ex.Message }, Cmd.none
  | LoadFailed ex -> { m with StatusMsg = sprintf "Loading failed with excption %s: %s" (ex.GetType().Name) ex.Message }, Cmd.none


let bindings () : Binding<Model, Msg> list = [
  "CurrentTime" |> Binding.oneWay (fun m -> m.CurrentTime)
  "Text" |> Binding.twoWay ((fun m -> m.Text), SetText)
  "StatusMsg" |> Binding.twoWay ((fun m -> m.StatusMsg), SetText)
  "Save" |> Binding.cmd RequestSave
  "Load" |> Binding.cmd RequestLoad
]


let designVm = ViewModel.designInstance (init () |> fst) (bindings ())


let timerTick dispatch =
  let timer = new Timers.Timer(1000.)
  timer.Elapsed.Add (fun _ -> dispatch (SetTime DateTimeOffset.Now))
  timer.Start()


let main window =

  let logger =
    LoggerConfiguration()
      .MinimumLevel.Override("Elmish.WPF.Update", Events.LogEventLevel.Verbose)
      .MinimumLevel.Override("Elmish.WPF.Bindings", Events.LogEventLevel.Verbose)
      .MinimumLevel.Override("Elmish.WPF.Performance", Events.LogEventLevel.Verbose)
      .WriteTo.Console()
      .CreateLogger()

  WpfProgram.mkProgram init update bindings
  |> WpfProgram.withSubscription (fun _ -> Cmd.ofSub timerTick)
  |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))
  |> WpfProgram.runWindow window
