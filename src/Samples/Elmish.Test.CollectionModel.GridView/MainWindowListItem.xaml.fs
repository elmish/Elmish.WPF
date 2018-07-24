namespace DataGrid

open FsXaml
open System.Windows
open System.Windows.Controls
open System.Collections.Specialized
open System.Windows.Data

type MainWindowListItemBase = XAML<"MainWindowListItem.xaml">

type MainWindowListItem() =
    inherit MainWindowListItemBase()
