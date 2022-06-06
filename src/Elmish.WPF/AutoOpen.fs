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


type OptionalBuilder() =
  member _.Bind(ma, f) =
    ma |> Option.bind f
  member _.Return(a) =
    Some a
  member _.ReturnFrom(ma) =
    ma
  member _.Delay(f) =
    f
  member _.Run(f) =
    f ()
  member this.Zero() =
    this.Return()
  member this.While(guard, body) =
    if not (guard ()) then this.Zero() else this.Bind(body (), (fun () -> this.While(guard, body)))
  member this.TryWith(body, handler) =
    try
      this.ReturnFrom(body ())
    with
    | e -> handler e
  member this.TryFinally(body, compensation) =
    try
      this.ReturnFrom(body ())
    finally
      compensation ()
  member this.Using(disposable: #System.IDisposable, body) =
    let body' = fun () -> body disposable

    this.TryFinally(
      body',
      fun () ->
        match disposable with
        | null -> ()
        | disp -> disp.Dispose()
    )
  member this.For(sequence: seq<_>, body) =
    this.Using(
      sequence.GetEnumerator(),
      fun enum -> this.While(enum.MoveNext, this.Delay(fun () -> body enum.Current))
    )

let option = OptionalBuilder()