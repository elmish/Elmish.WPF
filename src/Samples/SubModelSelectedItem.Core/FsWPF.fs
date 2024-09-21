namespace FsWPF

open System
open System.Windows.Data
open System.Globalization
open System.Windows
open System.Windows.Controls
open Elmish.WPF.Samples.SubModelSelectedItem.Program
open Form

type ComponentsTemplateSelector() =
    inherit DataTemplateSelector()

    member val TextBox_DT: DataTemplate = null with get, set
    member val CheckBox_DT: DataTemplate = null with get, set
    member val ComboBox_DT: DataTemplate = null with get, set

    override this.SelectTemplate(item: obj, container: DependencyObject) : DataTemplate =
        match item with
        | :? Form.Components as component_ ->
            match component_ with
            | Form.Components.TextBox _ -> this.TextBox_DT
            | Form.Components.CheckBox _ -> this.CheckBox_DT
            | Form.Components.ComboBox _ -> this.ComboBox_DT
        | _ -> null


// not working; idea taken from "https://github.com/elmish/Elmish.WPF/issues/270#issuecomment-687329493"
type GetComponentsVMToResource() =
    let toVM =
        function
        | TextBox a -> a |> box
        | CheckBox a -> a |> box
        | ComboBox a -> a |> box

    interface IValueConverter with
        member this.Convert
            (
                value: obj,
                targetType: System.Type,
                parameter: obj,
                culture: System.Globalization.CultureInfo
            ) : obj =
            match value with
            | :? list<Components> as components -> components |> List.map toVM |> box
            | _ -> failwith "shouldn't happen"

        member this.ConvertBack
            (
                value: obj,
                targetType: System.Type,
                parameter: obj,
                culture: System.Globalization.CultureInfo
            ) : obj =
            raise (System.NotImplementedException())