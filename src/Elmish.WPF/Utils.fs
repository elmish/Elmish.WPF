[<AutoOpen>]
module Elmish.WPF.Utils

open System.Collections.Generic

/// Wrapper for dictionary keys to support null/None keys
[<Struct>]
type private DictKey = K of obj

/// Memoizes the last return value of the function
let memoizeSingle f =
    let cache = Dictionary<_, _>()
    fun x ->
        let key = K x
        match cache.TryGetValue key with
        | true, v -> v
        | false, _ ->
            cache.Clear()
            let res = f x
            cache.[key] <- res
            res
