﻿[<AutoOpen>]
module Elmish.WPF.Utils


/// Reference/physical equality for reference types. Alias for
/// LanguagePrimitives.PhysicalEquality. Also see elmEq.
let refEq = LanguagePrimitives.PhysicalEquality


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
      Expression.Convert(Expression.Call(Expression.Convert(objExpr, method.DeclaringType), method), typeof<obj>),
      objExpr
    )

  let action = expr.Compile()
  fun target -> action.Invoke(target)


type private ElmEq<'a>() =

  static let gettersAndEq =
    typeof<'a>.GetProperties()
    |> Array.map (fun pi ->
      let getter = buildUntypedGetter pi

      let eq =
        if pi.PropertyType.IsValueType || pi.PropertyType = typeof<string> then
          (fun (a, b) -> a = b)
        else
          obj.ReferenceEquals

      getter, eq)

  static member Eq x1 x2 = gettersAndEq |> Array.forall (fun (get, eq) -> eq (get (box x1), get (box x2)))


/// Memberwise equality where value-typed members and string members are
/// compared using structural comparison (the standard F# (=) operator),
/// and reference-typed members (except strings) are compared using reference
/// equality. This is a useful default for lazy bindings since all parts of the
/// Elmish model (i.e., all members of the arguments to this function) are
/// normally immutable. For a direct reference equality check (not memberwise),
/// see refEq (which should be used when passing a single non-string reference
/// type from the model).
let elmEq<'a> : 'a -> 'a -> bool = ElmEq<'a>.Eq