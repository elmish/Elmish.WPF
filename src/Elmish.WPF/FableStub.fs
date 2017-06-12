[<AutoOpen>]
module Fable
    [<AutoOpen>]
    module Import =
        [<AutoOpen>] 
        module JS =
            type Promise<'T>() = 
                class end
                with
                    static member map _ = failwith "Promise not supported"
                    static member catch _ = failwith "Promise not supported"