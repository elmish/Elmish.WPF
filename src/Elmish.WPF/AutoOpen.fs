[<AutoOpen>]
module internal AutoOpen

open System
open System.Collections.Generic
open System.Threading.Tasks
open System.Windows.Threading


let flip f b a = f a b

let ignore2 _ _ = ()

/// Deconstructs a KeyValuePair into a tuple.
let (|Kvp|) (kvp: KeyValuePair<_,_>) =
  Kvp (kvp.Key, kvp.Value)


type Dispatcher with
  member this.InvokeIfActive(callback: Action) =
    try
      this.Invoke(callback)
    with :? TaskCanceledException -> ()
