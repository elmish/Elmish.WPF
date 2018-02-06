[<AutoOpen>]
module Elmish.WPF.Log

module console =
    let error (str,ex) = printfn "%s: %O" str ex
    let log o = printfn "%s -- %A" (System.DateTime.Now.ToString("o")) o