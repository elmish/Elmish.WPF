[<AutoOpen>]
module Fable
    [<AutoOpen>]
    module PowerPack = ()
    [<AutoOpen>]
    module Core = 
        [<AutoOpen>] 
        module JsInterop = ()
    [<AutoOpen>]
    module Import =
        [<AutoOpen>] 
        module Browser =
            [<AutoOpen>]
            module console =
                let error (str,ex) = printfn "%s: %O" str ex
                let log o = printfn "%A" o
                let toJson o = o
        [<AutoOpen>] 
        module JS =
            [<AutoOpen>]
            module JSON =
                let parse str = str
            type Promise<'T>() = 
                class end
                with
                    static member map _ = failwith "Promise not supported"
                    static member catch _ = failwith "Promise not supported"