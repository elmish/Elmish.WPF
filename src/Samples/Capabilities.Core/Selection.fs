module Elmish.WPF.Samples.Capabilities.Selection

open Elmish.WPF


type Selection =
  { SelectedIndex: int option
    SelectedIndexData: string list }

type SelectionMsg =
  | SetSelectedIndex of int option


module Selection =
  module SelectedIndex =
    let get m = m.SelectedIndex
    let set v m = { m with SelectedIndex = v }
  module SelectedIndexData =
    let get m = m.SelectedIndexData

  let init =
    { SelectedIndex = None
      SelectedIndexData = ["A"; "B"] }

  let update = function
    | SetSelectedIndex x -> x |> SelectedIndex.set

  let bindings () = [
    "SelectedIndex" |> Binding.twoWayOpt (SelectedIndex.get, SetSelectedIndex)
    "SelectedIndexData" |> Binding.oneWay SelectedIndexData.get
    "DeselectIndex" |> Binding.cmdIf (SelectedIndex.get >> Option.map (fun _ -> SetSelectedIndex None))
  ]