[<AutoOpen>]
module internal Elmish.WPF.InternalUtils


[<RequireQualifiedAccess>]
module Kvp =

  open System.Collections.Generic

  let key (kvp: KeyValuePair<_,_>) =
    kvp.Key

  let value (kvp: KeyValuePair<_,_>) =
    kvp.Value


[<RequireQualifiedAccess>]
module Result =

  let isOk = function
    | Ok _ -> true
    | Error _ -> false

  let iter f = function
    | Ok x -> f x
    | Error _ -> ()


[<RequireQualifiedAccess>]
module ValueOption =

  let ofOption = function
    | Some x -> ValueSome x
    | None -> ValueNone

  let toOption = function
    | ValueSome x -> Some x
    | ValueNone -> None

  let ofError = function
    | Ok _ -> ValueNone
    | Error x -> ValueSome x

  let ofOk = function
    | Ok x -> ValueSome x
    | Error _ -> ValueNone


[<RequireQualifiedAccess>]
module ByRefPair =

  let toOption (b, a) =
    if b then Some a else None


[<RequireQualifiedAccess>]
module Dictionary =

  open System.Collections.Generic

  let tryFind key (d: Dictionary<_, _>) =
    key |> d.TryGetValue |> ByRefPair.toOption


[<RequireQualifiedAccess>]
module IReadOnlyDictionary =

  open System.Collections.Generic

  let tryFind key (d: IReadOnlyDictionary<_, _>) =
    key |> d.TryGetValue |> ByRefPair.toOption


[<RequireQualifiedAccess>]
module Option =

  let fromBool a b =
    if b then Some a else None