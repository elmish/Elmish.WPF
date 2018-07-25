
namespace TreeViewModel

open FsXaml
open System.Windows.Data

type MainWindowBase = XAML<"MainWindow.xaml">

type MainWindow() =
    inherit MainWindowBase()

    member __.ReadonlyTreeView_SourceUpdated(sender:obj, e:DataTransferEventArgs) = 
        ()


    
