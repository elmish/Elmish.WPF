[<AutoOpen>]
module Utilities

let flip f b a = f a b

let map get set f a =
  a |> get |> f |> flip set a