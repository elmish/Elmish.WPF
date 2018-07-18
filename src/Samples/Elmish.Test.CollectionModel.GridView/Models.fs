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
        | RowChanged of int * SaleMessage

    type Model = SaleModel list
     
    let update msg (model:Model) =
        let model' = 
            match msg with
            | SomethingHappened -> model
            | RowChanged (i, m) -> 
                model
                |> List.mapi (fun j x -> if i = j then Sale.update m (model.[i]) else x)

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
            "Refresh" |> Binding.cmd (fun _ _ -> SomethingHappened)
            "Data" |> Binding.collectionModel subModels (fun () -> Sale.view) (fun k msg -> RowChanged (int k, msg))
            "Data2" |> Binding.oneWay (fun m -> m)
        ]
