module Elmish.WPF.Samples.SubModelOpt.Program

open Serilog
open Serilog.Extensions.Logging
open Elmish.WPF

module Form1 =
    type Model = { Text: string }

    type Msg =
        | SetText of string
        | Submit

    let init = { Text = "" }

    let update msg m =
        match msg with
        | SetText s -> { m with Text = s }
        | Submit -> m // handled by parent

[<AllowNullLiteral>]
type Form1ViewModel(args) =
    inherit ViewModelBase<Form1.Model, Form1.Msg>(args)

    let textBinding =
        Binding.TwoWayT.id
        >> Binding.addLazy (=)
        >> Binding.mapModel (fun (m: Form1.Model) -> m.Text)
        >> Binding.mapMsg Form1.SetText

    member this.Text
        with get () = base.Get () textBinding
        and set (value) = base.Set (value) textBinding

    member _.Submit = base.Get () (Binding.CmdT.setAlways Form1.Submit)

module Form2 =
    type Model = { Text1: string; Text2: string }

    type Msg =
        | SetText1 of string
        | SetText2 of string
        | Submit

    let init = { Text1 = ""; Text2 = "" }

    let update msg m =
        match msg with
        | SetText1 s -> { m with Text1 = s }
        | SetText2 s -> { m with Text2 = s }
        | Submit -> m // handled by parent

[<AllowNullLiteral>]
type Form2ViewModel(args) =
    inherit ViewModelBase<Form2.Model, Form2.Msg>(args)

    let text1Binding =
        Binding.TwoWayT.id
        >> Binding.addLazy (=)
        >> Binding.mapModel (fun (m: Form2.Model) -> m.Text1)
        >> Binding.mapMsg Form2.SetText1

    let text2Binding =
        Binding.TwoWayT.id
        >> Binding.addLazy (=)
        >> Binding.mapModel (fun (m: Form2.Model) -> m.Text2)
        >> Binding.mapMsg Form2.SetText2

    member this.Text1
        with get () = base.Get () text1Binding
        and set (value) = base.Set (value) text1Binding

    member this.Text2
        with get () = base.Get () text2Binding
        and set (value) = base.Set (value) text2Binding

    member _.Submit = base.Get () (Binding.CmdT.setAlways Form2.Submit)

module App =
    type Dialog =
        | Form1 of Form1.Model
        | Form2 of Form2.Model

    type Model = { Dialog: Dialog option }

    let init () = { Dialog = None }

    type Msg =
        | ShowForm1
        | ShowForm2
        | Form1Msg of Form1.Msg
        | Form2Msg of Form2.Msg

    let update msg m =
        match msg with
        | ShowForm1 ->
            { m with
                Dialog = Some <| Form1 Form1.init }
        | ShowForm2 ->
            { m with
                Dialog = Some <| Form2 Form2.init }
        | Form1Msg Form1.Submit -> { m with Dialog = None }
        | Form1Msg msg' ->
            match m.Dialog with
            | Some(Form1 m') ->
                { m with
                    Dialog = Form1.update msg' m' |> Form1 |> Some }
            | _ -> m
        | Form2Msg Form2.Submit -> { m with Dialog = None }
        | Form2Msg msg' ->
            match m.Dialog with
            | Some(Form2 m') ->
                { m with
                    Dialog = Form2.update msg' m' |> Form2 |> Some }
            | _ -> m

[<AllowNullLiteral>]
type MainViewModel(args) =
    inherit ViewModelBase<App.Model, App.Msg>(args)

    let createForm1Vm (args: ViewModelArgs<Form1.Model, Form1.Msg>) = Form1ViewModel(args)
    let createForm2Vm (args: ViewModelArgs<Form2.Model, Form2.Msg>) = Form2ViewModel(args)

    member _.ShowForm1 = base.Get () (Binding.CmdT.setAlways App.ShowForm1)
    member _.ShowForm2 = base.Get () (Binding.CmdT.setAlways App.ShowForm2)

    member _.DialogVisible =
        base.Get
            ()
            (Binding.OneWayT.id
             >> Binding.addLazy (=)
             >> Binding.mapModel (fun m -> m.Dialog.IsSome))

    member _.Form1Visible =
        base.Get
            ()
            (Binding.OneWayT.id
             >> Binding.addLazy (=)
             >> Binding.mapModel (fun m ->
                 match m.Dialog with
                 | Some(App.Form1 _) -> true
                 | _ -> false))

    member _.Form2Visible =
        base.Get
            ()
            (Binding.OneWayT.id
             >> Binding.addLazy (=)
             >> Binding.mapModel (fun m ->
                 match m.Dialog with
                 | Some(App.Form2 _) -> true
                 | _ -> false))

    member _.Form1 =
        base.Get
            ()
            (Binding.SubModelT.opt createForm1Vm
             >> Binding.mapModel (fun (m: App.Model) ->
                 match m.Dialog with
                 | Some(App.Form1 m') -> ValueSome m'
                 | _ -> ValueNone)
             >> Binding.mapMsg App.Form1Msg)

    member _.Form2 =
        base.Get
            ()
            (Binding.SubModelT.opt createForm2Vm
             >> Binding.mapModel (fun (m: App.Model) ->
                 match m.Dialog with
                 | Some(App.Form2 m') -> ValueSome m'
                 | _ -> ValueNone)
             >> Binding.mapMsg App.Form2Msg)

let main window =
    let logger =
        LoggerConfiguration()
            .MinimumLevel.Override("Elmish.WPF.Update", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Bindings", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Performance", Events.LogEventLevel.Verbose)
            .WriteTo.Console()
            .CreateLogger()

    let createVm args = MainViewModel(args)

    WpfProgram.mkSimpleT App.init App.update createVm
    |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))
    |> WpfProgram.startElmishLoop window