namespace DataGrid

open Elmish
open Elmish.WPF
open System
open System.Windows
open System.Collections.Generic
open System.Collections.ObjectModel

module Sale =
    type SaleMessage =
        | CountryChanged of string
        | RegionChanged of string
        | ItemTypeChanged of string
        | SaleChannelChanged of string
        | OrderDateChanged of DateTime
        | OrderIDChanged of int option
        | UnitSoldChanged of int option
        | UnitPriceChanged of decimal option
        | RevenueChanged of decimal option

    type SaleModel =
        {   Id          : int
            Country     : string
            Region      : string
            ItemType    : string
            SaleChannel : string
            OrderDate   : DateTime
            OrderID     : int option
            UnitSold    : int option
            UnitPrice   : decimal option
            Revenue     : decimal option    }
    with
        static member Default(id) =
            {   Id = id ; Country = "" ; Region = "" ; ItemType = "" ; SaleChannel = "" ; OrderDate = DateTime.Today ;
                OrderID = None ; UnitSold = None ; UnitPrice = None ; Revenue  = None ;     }

    let update msg subModel =
        let subModel' = 
            match msg with
            | CountryChanged v -> { subModel with Country = v }
            | RegionChanged v -> { subModel with Region = v }
            | ItemTypeChanged v -> { subModel with ItemType = v }
            | SaleChannelChanged v -> { subModel with SaleChannel = v }
            | OrderDateChanged v -> { subModel with OrderDate = v }
            | OrderIDChanged v -> { subModel with OrderID = v }
            | UnitSoldChanged v -> { subModel with UnitSold = v }
            | UnitPriceChanged v -> { subModel with UnitPrice = v }
            | RevenueChanged v -> { subModel with Revenue = v }
        subModel'
    
    let view = 
        [   "Id" |> Binding.oneWay (fun m -> m.Id)
            "Country" |> Binding.twoWay (fun m -> m.Country) (fun v m -> CountryChanged v)
            "Region" |> Binding.twoWay (fun m -> m.Region) (fun v m -> RegionChanged v)
            "ItemType" |> Binding.twoWay (fun m -> m.ItemType) (fun v m -> ItemTypeChanged v)
            "SaleChannel" |> Binding.twoWay (fun m -> m.SaleChannel) (fun v m -> SaleChannelChanged  v)
            "OrderDate" |> Binding.twoWay (fun m -> m.OrderDate) (fun v m -> OrderDateChanged v)
            "OrderID" |> Binding.twoWay (fun m -> m.OrderID) (fun v m -> OrderIDChanged v)
            "UnitSold" |> Binding.twoWay (fun m -> m.UnitSold) (fun v m -> UnitSoldChanged v)
            "UnitPrice" |> Binding.twoWay (fun m -> m.UnitPrice) (fun v m -> UnitPriceChanged v)
            "Revenue" |> Binding.twoWay (fun m -> m.Revenue) (fun v m -> RevenueChanged v)  ]

module Sales =    
    open Sale

    type Message = 
        | RowChanged of SaleModel * SaleMessage
        | RowDeleted of int
        | InsertRow of int

    type Model = ObservableCollection<SaleModel>

    let uiDispatch (f:unit-> unit) = Application.Current.Dispatcher.Invoke f

    let update msg (model:Model) =
        match msg with
            | RowDeleted i -> uiDispatch (fun _ -> model.RemoveAt i)
            | InsertRow i -> let newIndex = (model |> Seq.map (fun x -> x.Id) |> Seq.sortDescending |> Seq.item 0) + 1
                             uiDispatch (fun _ -> model.Insert (i + 1, SaleModel.Default(newIndex)))
            | RowChanged (submodel, msg') -> uiDispatch (fun _ -> model.[model.IndexOf submodel] <- Sale.update msg' submodel)
        model, Cmd.none

    let view _ _ =
        [   "Data" |> Binding.complexModel (fun m -> m :> IEnumerable<SaleModel>) (fun () -> Sale.view) RowChanged
            "DataMirror" |> Binding.complexModel (fun m -> m :> IEnumerable<SaleModel>) (fun () -> Sale.view) RowChanged 
            "DataMirrorSimple" |> Binding.oneWay (fun m -> m)
            "DeleteRow" |> Binding.cmd (fun x _ -> RowDeleted (x :?> int))
            "InsertRow" |> Binding.cmd (fun x _ -> InsertRow (x :?> int))   ]

module DataAccess =
    open Sale
    open FSharp.Data

    type Data = CsvProvider<"./Resources/5000 Sales Records.csv">

    let extractData () =
        let data = Data.Load("./Resources/5000 Sales Records.csv")

        let result = new ObservableCollection<_>()
        data.Rows
        //|> Seq.take 15
        |> Seq.iteri (fun i r ->
            result.Add {    Id = i
                            Country = r.Country
                            Region = r.Region
                            ItemType = r.``Item Type``
                            SaleChannel = r.``Sales Channel``
                            OrderDate = r.``Order Date``
                            OrderID = Some r.``Order ID``
                            UnitSold = Some r.``Units Sold``
                            UnitPrice = Some r.``Unit Price``
                            Revenue = Some r.``Total Revenue``})
        result

module App =
    open Sales

    let init() = DataAccess.extractData(), Cmd.none

    [<EntryPoint;STAThread>]
    let main argv = 
        Program.mkProgram init update view
        |> Program.withConsoleTrace
        |> Program.runWindow (MainWindowDataGrid())
        //|> Program.runWindow (MainWindowListItem())
