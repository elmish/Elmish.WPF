module Elmish.WPF.Samples.NewWindow.Window1Module

open Elmish.WPF

module Window1 =
    let init = ""

[<AllowNullLiteral>]
type Window1ViewModel(args) =
    inherit ViewModelBase<string, string>(args)

    member _.Input =
        base.Get () (Binding.TwoWayT.id >> Binding.mapModel id >> Binding.mapMsg id)