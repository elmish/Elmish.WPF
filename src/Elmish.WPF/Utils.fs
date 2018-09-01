namespace Elmish.WPF.Internal


[<AutoOpen>]
module Patterns =

  open System.Collections.Generic

  /// Deconstructs a KeyValuePair into a tuple.
  let (|Kvp|) (kvp: KeyValuePair<_,_>) =
    Kvp (kvp.Key, kvp.Value)


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
