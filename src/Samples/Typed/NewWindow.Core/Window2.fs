module Elmish.WPF.Samples.NewWindow.Window2Module

open Elmish.WPF

[<RequireQualifiedAccess>]
type ConfirmState =
    | Submit
    | Cancel
    | Close

type Window2 =
    { Input: string
      IsChecked: bool
      ConfirmState: ConfirmState option }

type Window2Msg =
    | SetInput of string
    | SetChecked of bool
    | Submit
    | Cancel
    | Close

[<RequireQualifiedAccess>]
type Window2OutMsg = | Close

module Window2 =
    module Input =
        let get m = m.Input
        let set v m = { m with Input = v }

    module IsChecked =
        let get m = m.IsChecked
        let set v m = { m with IsChecked = v }

    module ConfirmState =
        let set v m = { m with ConfirmState = v }

    let init =
        { Input = ""
          IsChecked = false
          ConfirmState = None }

    let update =
        function
        | SetInput s -> s |> Input.set
        | SetChecked b -> b |> IsChecked.set
        | Submit -> ConfirmState.Submit |> Some |> ConfirmState.set
        | Cancel -> ConfirmState.Cancel |> Some |> ConfirmState.set
        | Close -> ConfirmState.Close |> Some |> ConfirmState.set

    let confirmStateToMsg confirmState msg m =
        if m.ConfirmState = Some confirmState then
            InOut.Out Window2OutMsg.Close
        else
            InOut.In msg

[<AllowNullLiteral>]
type Window2ViewModel(args) =
    inherit ViewModelBase<Window2, InOut<Window2Msg, Window2OutMsg>>(args)

    member _.Input =
        base.Get
            ()
            (Binding.TwoWayT.id
             >> Binding.mapModel Window2.Input.get
             >> Binding.mapMsg (SetInput >> InOut.In))

    member _.IsChecked =
        base.Get
            ()
            (Binding.TwoWayT.id
             >> Binding.mapModel Window2.IsChecked.get
             >> Binding.mapMsg (SetChecked >> InOut.In))

    member _.SubmitMsgVisibility =
        base.Get
            ()
            (Binding.OneWayT.id
             >> Binding.mapModel (fun m -> m.ConfirmState = Some ConfirmState.Submit |> Bool.toVisibilityCollapsed))

    member _.CancelMsgVisibility =
        base.Get
            ()
            (Binding.OneWayT.id
             >> Binding.mapModel (fun m -> m.ConfirmState = Some ConfirmState.Cancel |> Bool.toVisibilityCollapsed))

    member _.CloseMsgVisibility =
        base.Get
            ()
            (Binding.OneWayT.id
             >> Binding.mapModel (fun m -> m.ConfirmState = Some ConfirmState.Close |> Bool.toVisibilityCollapsed))

    member _.Submit = base.Get () (Binding.CmdT.setAlways (InOut.In Submit))

    member _.Cancel = base.Get () (Binding.CmdT.setAlways (InOut.In Cancel))

    member _.Close = base.Get () (Binding.CmdT.setAlways (InOut.In Close))