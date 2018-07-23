namespace DataGrid

open FsXaml
open System.Windows
open System.Windows.Controls

type MainWindowBase = XAML<"MainWindow.xaml">

type MainWindow() =
    inherit MainWindowBase()
