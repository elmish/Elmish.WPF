namespace DataGrid

open Elmish
open Elmish.WPF

module Sale =
    open System

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
        {   Id: int
            Country: string
            Region: string
            ItemType: string
            SaleChannel: string
            OrderDate : DateTime
            OrderID : int option
            UnitSold : int option
            UnitPrice: decimal option
            Revenue: decimal option
            }

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
            "Revenue" |> Binding.twoWay (fun m -> m.Revenue) (fun v m -> RevenueChanged v)
            ]

module Sales =    
    open Sale

    type Message = 
        | SomethingHappened
        | RowChanged of IndexOrKey * SaleMessage

    type Model = SaleModel list
     
    let update msg (model:Model) =
        let model' = 
            match msg with
            | SomethingHappened -> model
            | RowChanged (i, m) -> 
                model
                |> List.mapi (fun j x -> if i = (Index j) then Sale.update m (model.[j]) else x)

        model', Cmd.none

    let toBoxList model = 
        model 
        |> List.mapi (fun i r -> string i, box r) 
        |> Map.ofList

    let toModel i (model:Model) =
        model.[i]
    
    let subModels (model:Model) = model |> Seq.ofList
    let subModel (model:Model) (key:string) = model.[int key]

    let view _ _ =
        [  
            "Data" |> Binding.collectionModel subModels (fun () -> Sale.view) (fun k msg -> RowChanged (k, msg))
            "Data2" |> Binding.oneWay (fun m -> m)
        ]

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

module App =
    open System
    open Sales

    [<EntryPoint;STAThread>]
    let main argv = 

        let init() = DataAccess.extractData(), Cmd.none
    
        Program.mkProgram init update view
        |> Program.withConsoleTrace
        |> Program.runWindow (MainWindow())
