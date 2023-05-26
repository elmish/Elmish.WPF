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

  let update = function
    | SetInput s -> s |> Input.set
    | SetChecked b -> b |> IsChecked.set
    | Submit -> ConfirmState.Submit |> Some |> ConfirmState.set
    | Cancel -> ConfirmState.Cancel |> Some |> ConfirmState.set
    | Close  -> ConfirmState.Close  |> Some |> ConfirmState.set

  let private confirmStateVisibilityBinding confirmState =
    fun m -> m.ConfirmState = Some confirmState
    >> Bool.toVisibilityCollapsed
    |> Binding.oneWay

  let private confirmStateToMsg confirmState msg m =
    if m.ConfirmState = Some confirmState
    then InOut.Out Window2OutMsg.Close
    else InOut.In msg

  let bindings () =
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

let designVm = ViewModel.designInstance Window2.init (Window2.bindings ())