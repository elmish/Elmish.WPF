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

// Properties for each DataTemplate
