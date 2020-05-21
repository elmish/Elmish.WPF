[<AutoOpen>]
module internal Elmish.WPF.Tests.InternalUtils


type InvokeTester<'a, 'b>(f: 'a -> 'b) =
  let mutable count = 0
  let mutable values = []
  let wrapped x =
    count <- count + 1
    values <- values @ [x]
    f x
  member __.Fn = wrapped
  member __.Count = count
  member __.Values = values
  member __.Reset () =
    count <- 0
    values <- []


type InvokeTester2<'a, 'b, 'c>(f: 'a -> 'b -> 'c) =
  let mutable count = 0
  let mutable values = []
  let wrapped x y =
    count <- count + 1
    values <- values @ [x, y]
    f x y
  member __.Fn = wrapped
  member __.Count = count
  member __.Values = values
  member __.Reset () =
    count <- 0
    values <- []


[<RequireQualifiedAccess>]
module String =

  let length (s: string) = s.Length


[<RequireQualifiedAccess>]
module List =

  let swap i j =
    List.permute
      (function
        | a when a = i -> j
        | a when a = j -> i
        | a -> a)

  let insert i a ma =
    (ma |> List.take i)
    @ [ a ]
    @ (ma |> List.skip i)

  let replace i a ma =
    (ma |> List.take i)
    @ [ a ]
    @ (ma |> List.skip (i + 1))
