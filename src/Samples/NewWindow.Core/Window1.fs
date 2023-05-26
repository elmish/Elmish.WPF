module Elmish.WPF.Samples.NewWindow.Window1Module

open Elmish.WPF


module Window1 =
  let init = ""

  let bindings () = [
    "Input" |> Binding.twoWay (id, id)
  ]

let designVm = ViewModel.designInstance Window1.init (Window1.bindings ())