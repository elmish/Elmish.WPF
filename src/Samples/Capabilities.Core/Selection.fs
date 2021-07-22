module Elmish.WPF.Samples.Capabilities.Selection

open Elmish.WPF


type Tree<'a> =
  { Data: 'a
    Children: Tree<'a> list }

module Tree =
  let create a ma = { Data = a; Children = ma }
  let createLeaf a = create a []
  module Data =
    let get m = m.Data
  module Children =
    let get m = m.Children


type Selection =
  { SelectedIndex: int option
    SelectedIndexData: string list
    SelectedValue: string option
    SelectedValueData: Tree<string> list }

type SelectionMsg =
  | SetSelectedIndex of int option
  | SetSelectedValue of string option


module Selection =
  module SelectedIndex =
    let get m = m.SelectedIndex
    let set v m = { m with SelectedIndex = v }
  module SelectedIndexData =
    let get m = m.SelectedIndexData
  module SelectedValue =
    let get m = m.SelectedValue
    let set v m = { m with SelectedValue = v }
  module SelectedValueData =
    let get m = m.SelectedValueData

  let init =
    { SelectedIndex = None
      SelectedIndexData = ["A"; "B"]
      SelectedValue = None
      SelectedValueData =
        [ Tree.create "A" [ Tree.createLeaf "Aa"; Tree.createLeaf "Ab" ]
          Tree.create "B" [ Tree.createLeaf "Ba"; Tree.createLeaf "Bb" ] ] }

  let update = function
    | SetSelectedIndex x -> x |> SelectedIndex.set
    | SetSelectedValue x -> x |> SelectedValue.set

  let rec recursiveSelectedValueBindings () = [
    "Data" |> Binding.oneWay Tree.Data.get
    "SelectedValueChildren"
      |> Binding.subModelSeq recursiveSelectedValueBindings
      |> Binding.mapModel (Tree.Children.get >> List.toSeq)
      |> Binding.mapMsg snd
  ]

  let bindings () = [
    "SelectedIndex" |> Binding.twoWay (SelectedIndex.get >> Option.defaultValue -1, Some >> Option.filter ((<=) 0) >> SetSelectedIndex)
    "DeselectIndex" |> Binding.cmdIf (SelectedIndex.get >> Option.map (fun _ -> SetSelectedIndex None))
    "SelectedIndexData" |> Binding.oneWay SelectedIndexData.get

    "SelectedValue" |> Binding.twoWayOpt (SelectedValue.get, SetSelectedValue)
    "SelectedValueData"
      |> Binding.subModelSeq recursiveSelectedValueBindings
      |> Binding.mapModel (SelectedValueData.get >> List.toSeq)
      |> Binding.mapMsg snd
  ]