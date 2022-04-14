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


[<RequireQualifiedAccess>]
module SeqOption =

  let somes mma = mma |> Seq.choose id


[<RequireQualifiedAccess>]
module Pair =

  open System.Collections.Generic

  let ofKvp (kvp: KeyValuePair<_,_>) = (kvp.Key, kvp.Value)

  let mapAll f g (a, c) = (f a, g c)

  let map2 f (a, c) = (a, f c)


[<RequireQualifiedAccess>]
module PairOption =

  let sequence = function
    | Some a, Some b -> Some (a, b)
    | _ -> None


[<RequireQualifiedAccess>]
module Func2 =

  let id1<'a, 'b> (a: 'a) (_: 'b) = a
  let id2<'a, 'b> (_: 'a) (b: 'b) = b
  let curry f a b = f (a, b)


[<RequireQualifiedAccess>]
module Func3 =
  let curry f a b c = f (a, b, c)


[<RequireQualifiedAccess>]
module Func5 =
  let curry f a b c d e = f (a, b, c, d, e)