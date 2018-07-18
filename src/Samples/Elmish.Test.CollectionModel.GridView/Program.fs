open System
        
open Elmish
open Elmish.WPF
open DataGrid
open Sales   
open FSharp.Data


type Data = CsvProvider<"./Resources/5000 Sales Records.csv">

module DataAccess =
    open Sale

    let extractData () =
        let data = Data.Load("./Resources/5000 Sales Records.csv")

        data.Rows
        |> Seq.mapi (fun i r ->
            {   Id=i
                Country = r.Country
                Region = r.Region
                ItemType = r.``Item Type``
                SaleChannel = r.``Sales Channel``
                OrderDate = r.``Order Date``
                OrderID = Some r.``Order ID``
                UnitSold = Some r.``Units Sold``
                UnitPrice = Some r.``Unit Price``
                Revenue = Some r.``Total Revenue``})
        |> Seq.toList                  

[<EntryPoint;STAThread>]
let main argv = 

    let init() = DataAccess.extractData(), Cmd.none
    
    Program.mkProgram init update view
    |> Program.withConsoleTrace
    |> Program.runWindow (MainWindow())