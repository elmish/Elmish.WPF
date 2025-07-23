module Elmish.WPF.Samples.Capabilities.Selection

open Elmish.WPF

type Tree<'a> = { Data: 'a; Children: Tree<'a> list }

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
          SelectedIndexData = [ "A"; "B" ]
          SelectedValue = None
          SelectedValueData =
            [ Tree.create "A" [ Tree.createLeaf "Aa"; Tree.createLeaf "Ab" ]
              Tree.create "B" [ Tree.createLeaf "Ba"; Tree.createLeaf "Bb" ] ] }

    let update =
        function
        | SetSelectedIndex x -> x |> SelectedIndex.set
        | SetSelectedValue x -> x |> SelectedValue.set

[<AllowNullLiteral>]
type TreeViewModel(args) =
    inherit ViewModelBase<Tree<string>, unit>(args)

    let createTreeVm (args: ViewModelArgs<Tree<string>, unit>) = TreeViewModel(args)

    member _.Data = base.Get () (Binding.OneWayT.id >> Binding.mapModel Tree.Data.get)

    member _.SelectedValueChildren =
        base.Get
            ()
            (Binding.SubModelSeqUnkeyedT.id createTreeVm
             >> Binding.mapModel (Tree.Children.get >> List.ofSeq)
             >> Binding.mapMsg snd)

[<AllowNullLiteral>]
type SelectionViewModel(args) =
    inherit ViewModelBase<Selection, SelectionMsg>(args)

    let createTreeVm (args: ViewModelArgs<Tree<string>, unit>) = TreeViewModel(args)

    let selectedIndexBinding =
        Binding.TwoWayT.id
        >> Binding.addLazy (=)
        >> Binding.mapModel (fun (m: Selection) -> m.SelectedIndex |> Option.defaultValue -1)
        >> Binding.mapMsg (fun v -> SetSelectedIndex(if v = -1 then None else Some v))

    let selectedValueBinding =
        Binding.TwoWayT.id
        >> Binding.addLazy (=)
        >> Binding.mapModel (fun (m: Selection) -> m.SelectedValue |> Option.defaultValue "")
        >> Binding.mapMsg (fun v -> SetSelectedValue(if v = "" then None else Some v))

    member this.SelectedIndex
        with get () = base.Get () selectedIndexBinding
        and set (value) = base.Set (value) selectedIndexBinding

    member _.DeselectIndex =
        base.Get () (Binding.CmdT.set (fun m -> m.SelectedIndex.IsSome) (SetSelectedIndex None))

    member _.SelectedIndexData =
        base.Get () (Binding.OneWayT.id >> Binding.mapModel Selection.SelectedIndexData.get)

    member this.SelectedValue
        with get () = base.Get () selectedValueBinding
        and set (value) = base.Set (value) selectedValueBinding

    member _.SelectedValueData =
        base.Get
            ()
            (Binding.SubModelSeqUnkeyedT.id createTreeVm
             >> Binding.mapModel (Selection.SelectedValueData.get >> List.ofSeq)
             >> Binding.mapMsg (fun _ -> failwith "TreeViewModel should not send messages"))