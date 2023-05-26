module Elmish.WPF.Samples.NewWindow.AppModule

open System.Windows

open Elmish.WPF

open Window1Module
open Window2Module


type App =
  { Window1: WindowState<string>
    Window2: Window2 option }

type AppMsg =
  | Window1Show
  | Window1Hide
  | Window1Close
  | Window1SetInput of string
  | Window2Show
  | Window2Close
  | Window2Msg of Window2Msg


module App =
  module Window1 =
    let get m = m.Window1
    let set v m = { m with Window1 = v }
    let map = map get set
  module Window2 =
    let get m = m.Window2
    let set v m = { m with Window2 = v }
    let map = map get set
    let mapOutMsg = function
      | Window2OutMsg.Close -> Window2Close
    let mapInOutMsg = InOut.cata Window2Msg mapOutMsg

  let init =
    { Window1 = WindowState.Closed
      Window2 = None }

  let update = function
    | Window1Show -> "" |> WindowState.toVisible |> Window1.map
    | Window1Hide -> "" |> WindowState.toHidden  |> Window1.map
    | Window1Close -> WindowState.Closed |> Window1.set
    | Window1SetInput s -> s |> WindowState.set |> Window1.map
    | Window2Show -> Window2.init |> Some |> Window2.set
    | Window2Close -> None |> Window2.set
    | Window2Msg msg -> msg |> Window2.update |> Option.map |> Window2.map

  let bindings (createWindow1: unit -> #Window) (createWindow2: unit -> #Window) () = [
    "Window1Show" |> Binding.cmd Window1Show
    "Window1Hide" |> Binding.cmd Window1Hide
    "Window1Close" |> Binding.cmd Window1Close
    "Window2Show" |> Binding.cmd Window2Show
    "Window1" |> Binding.subModelWin(
      (fun m -> m.Window1),
      snd,
      id,
      Window1.bindings >> Bindings.mapMsg Window1SetInput,
      createWindow1)
    "Window2" |> Binding.subModelWin(
      Window2.get >> WindowState.ofOption,
      snd,
      Window2.mapInOutMsg,
      Window2.bindings,
      createWindow2,
      isModal = true)
  ]

let private fail _ = failwith "never called"
let designVm = ViewModel.designInstance App.init (App.bindings fail fail ())