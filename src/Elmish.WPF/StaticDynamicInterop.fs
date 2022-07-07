namespace Elmish.WPF

open System.Runtime.CompilerServices

module StaticDynamicInterop =

  module Binding =
    let toBindingT binding = { DataT = binding.Data; Name = binding.Name }

  module BindingT =
    let toBinding bindingT = { Data = bindingT.DataT |> BindingData.boxT; Name = bindingT.Name }

  type StaticHelper<'model, 'msg> with

    member x.GetBinding ([<CallerMemberName>] ?memberName: string) =
      fun (binding: string -> Binding<'model, 'msg>) ->
        binding >> Binding.toBindingT
        |> x.Get(?memberName = memberName)

    member x.SetBinding (value, [<CallerMemberName>] ?memberName: string) =
      fun (binding: string -> Binding<'model, 'msg>) ->
        binding >> Binding.toBindingT
        |> x.Set(value, ?memberName = memberName)
