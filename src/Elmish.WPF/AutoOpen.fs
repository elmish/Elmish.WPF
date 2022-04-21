[<AutoOpen>]
module internal AutoOpen

open System.Collections.Generic
open System.Diagnostics

open System
open Microsoft.FSharp.Reflection
open Microsoft.FSharp.Quotations.Patterns

/// Evaluates expression untyped
let rec eval = function
    | Value(v,_t) -> v
    | Coerce(e,_t) -> eval e
    | NewObject(ci,args) -> ci.Invoke(evalAll args)
    | NewArray(t,args) -> 
        let array = Array.CreateInstance(t, args.Length) 
        args |> List.iteri (fun i arg -> array.SetValue(eval arg, i))
        box array
    | NewUnionCase(case,args) -> FSharpValue.MakeUnion(case, evalAll args)
    | NewRecord(t,args) -> FSharpValue.MakeRecord(t, evalAll args)
    | NewTuple(args) ->
        let t = FSharpType.MakeTupleType [|for arg in args -> arg.Type|]
        FSharpValue.MakeTuple(evalAll args, t)
    | FieldGet(Some(Value(v,_)),fi) -> fi.GetValue(v)
    | PropertyGet(None, pi, args) -> pi.GetValue(null, evalAll args)
    | PropertyGet(Some(x),pi,args) -> pi.GetValue(eval x, evalAll args)
    | Call(None,mi,args) -> mi.Invoke(null, evalAll args)
    | Call(Some(x),mi,args) -> mi.Invoke(eval x, evalAll args)
    | arg -> raise <| NotSupportedException(arg.ToString())
and evalAll args = [|for arg in args -> eval arg|]


let flip f b a = f a b

let ignore2 _ _ = ()

/// Deconstructs a KeyValuePair into a tuple.
[<DebuggerStepThrough>]
let (|Kvp|) (kvp: KeyValuePair<_,_>) =
  Kvp (kvp.Key, kvp.Value)

  
[<Struct>]
type OptionalBuilder =
  member _.Bind(ma, f) =
    ma |> Option.bind f
  member _.Return(a) =
    Some a
  member _.ReturnFrom(ma) =
    ma
    
let option = OptionalBuilder()