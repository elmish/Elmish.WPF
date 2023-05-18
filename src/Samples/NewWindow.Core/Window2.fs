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
type Window2OutMsg =
  | Close


module Window2 =
  module Input =
    let get (m: Window2) : string = m.Input
    let set (v: string) (m: Window2) : Window2 = { m with Input = v }
  module IsChecked =
    let get (m: Window2) : bool = m.IsChecked
    let set (v: bool) (m:Window2) : Window2 = { m with IsChecked = v }
  module ConfirmState =
    let set (v: ConfirmState option) (m: Window2) : Window2 = { m with ConfirmState = v }

  let init : Window2 =
    { Input = ""
      IsChecked = false
      ConfirmState = None }

  let update (msg: Window2Msg) : (Window2 -> Window2) =
    match msg with
    | SetInput s -> s |> Input.set
    | SetChecked b -> b |> IsChecked.set
    | Submit -> ConfirmState.Submit |> Some |> ConfirmState.set
    | Cancel -> ConfirmState.Cancel |> Some |> ConfirmState.set
    | Close  -> ConfirmState.Close  |> Some |> ConfirmState.set

  let private confirmStateVisibilityBinding (confirmState: ConfirmState) : (string -> Binding<Window2,'a>) =
    fun m -> m.ConfirmState = Some confirmState
    >> Bool.toVisibilityCollapsed
    |> Binding.oneWay

  let private confirmStateToMsg (confirmState: ConfirmState) (msg: 'a) (m: Window2) : InOut<'a,Window2OutMsg> =
    if m.ConfirmState = Some confirmState
    then InOut.Out Window2OutMsg.Close
    else InOut.In msg

  let bindings unit : Binding<Window2,InOut<Window2Msg,Window2OutMsg>,obj> list =
    let inBindings =
      [ "Input" |> Binding.twoWay (Input.get, SetInput)
        "IsChecked" |> Binding.twoWay (IsChecked.get, SetChecked)
        "SubmitMsgVisibility" |> confirmStateVisibilityBinding ConfirmState.Submit
        "CancelMsgVisibility" |> confirmStateVisibilityBinding ConfirmState.Cancel
        "CloseMsgVisibility"  |> confirmStateVisibilityBinding ConfirmState.Close ]
      |> Bindings.mapMsg InOut.In
    let inOutBindings =
      [ "Submit" |> Binding.cmd (confirmStateToMsg ConfirmState.Submit Submit)
        "Cancel" |> Binding.cmd (confirmStateToMsg ConfirmState.Cancel Cancel)
        "Close"  |> Binding.cmd (confirmStateToMsg ConfirmState.Close  Close) ]
    inBindings @ inOutBindings

let designVm : obj = ViewModel.designInstance Window2.init (Window2.bindings ())