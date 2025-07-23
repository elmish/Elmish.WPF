module Elmish.WPF.Samples.FileDialogsCmdMsg.Program

open System
open System.IO
open Serilog
open Serilog.Extensions.Logging
open Elmish
open Elmish.WPF


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


let save text =
    async {
        let dlg = Microsoft.Win32.SaveFileDialog()
        dlg.Filter <- "Text file (*.txt)|*.txt|Markdown file (*.md)|*.md"
        let result = dlg.ShowDialog()

        if result.HasValue && result.Value then
            do! File.WriteAllTextAsync(dlg.FileName, text) |> Async.AwaitTask
            return SaveSuccess
        else
            return SaveCanceled
    }


let load () =
    async {
        let dlg = Microsoft.Win32.OpenFileDialog()
        dlg.Filter <- "Text file (*.txt)|*.txt|Markdown file (*.md)|*.md"
        dlg.DefaultExt <- "txt"
        let result = dlg.ShowDialog()

        if result.HasValue && result.Value then
            let! contents = File.ReadAllTextAsync(dlg.FileName) |> Async.AwaitTask
            return LoadSuccess contents
        else
            return LoadCanceled
    }


let toCmd =
    function
    | Save text -> Cmd.OfAsync.either save text id SaveFailed
    | Load -> Cmd.OfAsync.either load () id LoadFailed


let update msg m =
    match msg with
    | SetTime t -> { m with CurrentTime = t }, []
    | SetText s -> { m with Text = s }, []
    | RequestSave -> m, [ Save m.Text ]
    | RequestLoad -> m, [ Load ]
    | SaveSuccess ->
        { m with
            StatusMsg = sprintf "Successfully saved at %O" DateTimeOffset.Now },
        []
    | LoadSuccess s ->
        { m with
            Text = s
            StatusMsg = sprintf "Successfully loaded at %O" DateTimeOffset.Now },
        []
    | SaveCanceled -> { m with StatusMsg = "Saving canceled" }, []
    | LoadCanceled ->
        { m with
            StatusMsg = "Loading canceled" },
        []
    | SaveFailed ex ->
        { m with
            StatusMsg = sprintf "Saving failed with exception %s: %s" (ex.GetType().Name) ex.Message },
        []
    | LoadFailed ex ->
        { m with
            StatusMsg = sprintf "Loading failed with exception %s: %s" (ex.GetType().Name) ex.Message },
        []


[<AllowNullLiteral>]
type FileDialogsCmdMsgViewModel(args) =
    inherit ViewModelBase<Model, Msg>(args)

    let textBinding =
        Binding.TwoWayT.id
        >> Binding.addLazy (=)
        >> Binding.mapModel (fun m -> m.Text)
        >> Binding.mapMsg SetText

    member _.CurrentTime =
        base.Get
            ()
            (Binding.OneWayT.id
             >> Binding.addLazy (=)
             >> Binding.mapModel (fun m -> m.CurrentTime))

    member this.Text
        with get () = base.Get () textBinding
        and set (value) = base.Set (value) textBinding

    member _.StatusMsg =
        base.Get
            ()
            (Binding.OneWayT.id
             >> Binding.addLazy (=)
             >> Binding.mapModel (fun m -> m.StatusMsg))

    member _.Save = base.Get () (Binding.CmdT.setAlways RequestSave)

    member _.Load = base.Get () (Binding.CmdT.setAlways RequestLoad)


let subscriptions (_model: Model) : Sub<Msg> =
    let timerTickSub (dispatch: Msg -> unit) : IDisposable =
        let timer = new Timers.Timer(1000.)
        let disp = timer.Elapsed.Subscribe(fun _ -> dispatch (SetTime DateTimeOffset.Now))
        timer.Start()
        disp

    [ [ nameof timerTickSub ], timerTickSub ]

let main window =
    let logger =
        LoggerConfiguration()
            .MinimumLevel.Override("Elmish.WPF.Update", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Bindings", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Performance", Events.LogEventLevel.Verbose)
            .WriteTo.Console()
            .CreateLogger()

    let createVm args = FileDialogsCmdMsgViewModel(args)

    WpfProgram.mkProgramWithCmdMsgT init update createVm toCmd
    |> WpfProgram.withSubscription subscriptions
    |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))
    |> WpfProgram.startElmishLoop window