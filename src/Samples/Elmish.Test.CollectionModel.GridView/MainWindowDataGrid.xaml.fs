namespace DataGrid

open FsXaml
open System.Windows
open System.Windows.Controls
open System.Collections.Specialized

type MainWindowDataGridBase = XAML<"MainWindowDataGrid.xaml">

type MainWindowDataGrid() =
    inherit MainWindowDataGridBase()

    