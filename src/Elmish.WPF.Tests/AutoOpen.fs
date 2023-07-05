[<AutoOpen>]
module AutoOpen


type InvokeTester<'a, 'b>(f: 'a -> 'b) =
  let mutable count = 0
  let mutable values = []

  let wrapped a =
    count <- count + 1
    values <- values @ [ a ]
    f a

  member __.Fn = wrapped
  member __.Count = count
  member __.Values = values

  member __.Reset() =
    count <- 0
    values <- []


type InvokeTester2<'a, 'b, 'c>(f: 'a -> 'b -> 'c) =
  let mutable count = 0
  let mutable values = []

  let wrapped a b =
    count <- count + 1
    values <- values @ [ a, b ]
    f a b

  member __.Fn = wrapped
  member __.Count = count
  member __.Values = values

  member __.Reset() =
    count <- 0
    values <- []


type InvokeTester3<'a, 'b, 'c, 'd>(f: 'a -> 'b -> 'c -> 'd) =
  let mutable count = 0
  let mutable values = []

  let wrapped a b c =
    count <- count + 1
    values <- values @ [ a, b, c ]
    f a b c

  member __.Fn = wrapped
  member __.Count = count
  member __.Values = values

  member __.Reset() =
    count <- 0
    values <- []


[<RequireQualifiedAccess>]
module String =

  let length (s: string) = s.Length


[<RequireQualifiedAccess>]
module List =

  let swap i j =
    List.permute (function
      | a when a = i -> j
      | a when a = j -> i
      | a -> a)

  let insert i a ma =
    (ma |> List.take i) @ [ a ] @ (ma |> List.skip i)

  let replace i a ma =
    (ma |> List.take i) @ [ a ] @ (ma |> List.skip (i + 1))