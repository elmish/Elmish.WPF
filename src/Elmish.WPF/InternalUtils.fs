[<AutoOpen>]
module internal Elmish.WPF.InternalUtils


open System
open System.Linq.Expressions
open System.Reflection

/// Returns a fast, untyped getter for the property specified by the PropertyInfo.
/// The getter takes an instance and returns a property value.
let buildUntypedGetter (propertyInfo: PropertyInfo) : obj -> obj =
  let method = propertyInfo.GetMethod
  let objExpr = Expression.Parameter(typeof<obj>, "o")
  let expr =
    Expression.Lambda<Func<obj, obj>>(
      Expression.Convert(
        Expression.Call(
          Expression.Convert(objExpr, method.DeclaringType), method),
          typeof<obj>),
      objExpr)
  let action = expr.Compile()
  fun target -> action.Invoke(target)


[<AutoOpen>]
module Patterns =

  open System.Collections.Generic

  /// Deconstructs a KeyValuePair into a tuple.
  let (|Kvp|) (kvp: KeyValuePair<_,_>) =
    Kvp (kvp.Key, kvp.Value)


[<RequireQualifiedAccess>]
module Kvp =

  open System.Collections.Generic

  let key (kvp: KeyValuePair<_,_>) =
    kvp.Key

  let value (kvp: KeyValuePair<_,_>) =
    kvp.Value


[<RequireQualifiedAccess>]
module Result =

  let isOk = function
    | Ok _ -> true
    | Error _ -> false

  let iter f = function
    | Ok x -> f x
    | Error _ -> ()


[<RequireQualifiedAccess>]
module ValueOption =

  let ofOption = function
    | Some x -> ValueSome x
    | None -> ValueNone

  let toOption = function
  | ValueSome x -> Some x
  | ValueNone -> None

  let ofError = function
    | Ok _ -> ValueNone
    | Error x -> ValueSome x

  let ofOk = function
  | Ok x -> ValueSome x
  | Error _ -> ValueNone
