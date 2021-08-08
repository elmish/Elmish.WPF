[<AutoOpen>]
module internal AutoOpen

open System.Collections.Generic
open System.Diagnostics


let flip f b a = f a b

let ignore2 _ _ = ()

/// Deconstructs a KeyValuePair into a tuple.
[<DebuggerStepThrough>]
let (|Kvp|) (kvp: KeyValuePair<_,_>) =
  Kvp (kvp.Key, kvp.Value)