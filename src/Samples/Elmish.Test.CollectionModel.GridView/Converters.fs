namespace DataGrid

open System.Windows.Data
open System


type IntOptionConverter() =
    interface IValueConverter with
        member __.Convert(value, targetType, parameters, culture) =
            match unbox<int option> value with
            | None -> ""
            | Some v -> string v
            |> box
        member __.ConvertBack(value, targetType, parameters, culture) =
            match System.Int32.TryParse(string value) with
            | (true, v) -> Some v
            | (false, _) -> None
            |> box


type DecimalOptionConverter() =
    interface IValueConverter with
        member __.Convert(value, targetType, parameters, culture) =
            match unbox<Decimal option> value with
            | None -> ""
            | Some v -> string v
            |> box
        member __.ConvertBack(value, targetType, parameters, culture) =
            match System.Decimal.TryParse(string value) with
            | (true, v) -> Some v
            | (false, _) -> None
            |> box
