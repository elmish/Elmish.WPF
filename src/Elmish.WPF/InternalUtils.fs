[<AutoOpen>]
module internal Elmish.WPF.InternalUtils

open System.Collections.Generic
open System.Diagnostics


/// <summary>
/// Flips the order of arguments for a two-argument function.
/// </summary>
/// <param name="f">The function to flip.</param>
/// <param name="b">The second argument.</param>
/// <param name="a">The first argument.</param>
/// <returns>The result of f a b.</returns>
let flip f b a = f a b

/// <summary>
/// Ignores two arguments and returns unit.
/// </summary>
/// <param name="_">First ignored argument.</param>
/// <param name="_">Second ignored argument.</param>
let ignore2 _ _ = ()

/// <summary>
/// Deconstructs a KeyValuePair into a tuple.
/// </summary>
/// <param name="kvp">The KeyValuePair to deconstruct.</param>
/// <returns>A tuple of (key, value).</returns>
[<DebuggerStepThrough>]
let (|Kvp|) (kvp: KeyValuePair<_, _>) = Kvp(kvp.Key, kvp.Value)


/// <summary>
/// Computation expression builder for Option monad.
/// </summary>
[<Struct>]
type OptionalBuilder =
    member _.Bind(ma, f) = ma |> Option.bind f
    member _.Return(a) = Some a
    member _.ReturnFrom(ma) = ma

/// <summary>
/// Instance of the optional computation expression builder.
/// </summary>
let option = OptionalBuilder()


/// <summary>
/// Utility functions for KeyValuePair.
/// </summary>
[<RequireQualifiedAccess>]
module Kvp =

    /// <summary>
    /// Gets the key from a KeyValuePair.
    /// </summary>
    /// <param name="kvp">The KeyValuePair.</param>
    /// <returns>The key.</returns>
    let key (kvp: KeyValuePair<_, _>) = kvp.Key

    /// <summary>
    /// Gets the value from a KeyValuePair.
    /// </summary>
    /// <param name="kvp">The KeyValuePair.</param>
    /// <returns>The value.</returns>
    let value (kvp: KeyValuePair<_, _>) = kvp.Value


/// <summary>
/// Utility functions for Result type.
/// </summary>
[<RequireQualifiedAccess>]
module Result =

    /// <summary>
    /// Checks if a Result is Ok.
    /// </summary>
    /// <param name="result">The Result to check.</param>
    /// <returns>True if Ok, false if Error.</returns>
    let isOk =
        function
        | Ok _ -> true
        | Error _ -> false

    /// <summary>
    /// Applies a function to the Ok value if present, otherwise does nothing.
    /// </summary>
    /// <param name="f">The function to apply.</param>
    /// <param name="result">The Result value.</param>
    let iter f =
        function
        | Ok x -> f x
        | Error _ -> ()


/// <summary>
/// Utility functions for ValueOption type.
/// </summary>
[<RequireQualifiedAccess>]
module ValueOption =

    /// <summary>
    /// Converts an Option to a ValueOption.
    /// </summary>
    let ofOption =
        function
        | Some x -> ValueSome x
        | None -> ValueNone

    /// <summary>
    /// Converts a ValueOption to an Option.
    /// </summary>
    let toOption =
        function
        | ValueSome x -> Some x
        | ValueNone -> None

    /// <summary>
    /// Extracts the Error value as a ValueOption, returning ValueNone for Ok.
    /// </summary>
    let ofError =
        function
        | Ok _ -> ValueNone
        | Error x -> ValueSome x

    /// <summary>
    /// Extracts the Ok value as a ValueOption, returning ValueNone for Error.
    /// </summary>
    let ofOk =
        function
        | Ok x -> ValueSome x
        | Error _ -> ValueNone

    [<RequireQualifiedAccess>]
    type ToNullError = ValueCannotBeNull of string

    /// <summary>
    /// Converts a nullable value to a ValueOption.
    /// </summary>
    /// <param name="x">The value to convert.</param>
    /// <returns>ValueSome if not null, ValueNone otherwise.</returns>
    let ofNull<'a> (x: 'a) =
        match box x with
        | null -> ValueNone
        | _ -> ValueSome x

    /// <summary>
    /// Converts a ValueOption to a nullable value.
    /// </summary>
    /// <returns>Ok with the value or null, Error if the type cannot be null.</returns>
    let toNull<'a> =
        function
        | ValueSome x -> Ok x
        | ValueNone ->
            let default' = Unchecked.defaultof<'a>

            if box default' = null then
                default' |> Ok
            else
                typeof<'a>.Name |> ToNullError.ValueCannotBeNull |> Error


/// <summary>
/// Utility functions for by-reference pairs.
/// </summary>
[<RequireQualifiedAccess>]
module ByRefPair =

    /// <summary>
    /// Converts a bool * 'a pair to an Option.
    /// </summary>
    /// <param name="b">The boolean flag.</param>
    /// <param name="a">The value.</param>
    /// <returns>Some a if b is true, None otherwise.</returns>
    let toOption (b, a) = if b then Some a else None


/// <summary>
/// Utility functions for Dictionary.
/// </summary>
[<RequireQualifiedAccess>]
module Dictionary =

    /// <summary>
    /// Tries to find a value in the dictionary.
    /// </summary>
    /// <param name="key">The key to search for.</param>
    /// <param name="d">The dictionary.</param>
    /// <returns>Some value if found, None otherwise.</returns>
    let tryFind key (d: Dictionary<_, _>) =
        key |> d.TryGetValue |> ByRefPair.toOption


[<RequireQualifiedAccess>]
module IReadOnlyDictionary =

    let tryFind key (d: IReadOnlyDictionary<_, _>) =
        key |> d.TryGetValue |> ByRefPair.toOption


[<RequireQualifiedAccess>]
module Option =

    let fromBool a b = if b then Some a else None


[<RequireQualifiedAccess>]
module SeqOption =

    let somes mma = mma |> Seq.choose id


[<RequireQualifiedAccess>]
module Pair =

    let ofKvp (kvp: KeyValuePair<_, _>) = (kvp.Key, kvp.Value)

    let mapAll f g (a, c) = (f a, g c)

    let map2 f (a, c) = (a, f c)


[<RequireQualifiedAccess>]
module PairOption =

    let sequence =
        function
        | Some a, Some b -> Some(a, b)
        | _ -> None


[<RequireQualifiedAccess>]
module Func2 =

    let id1<'a, 'b> (a: 'a) (_: 'b) = a
    let id2<'a, 'b> (_: 'a) (b: 'b) = b
    let curry f a b = f (a, b)


[<RequireQualifiedAccess>]
module Func3 =
    let curry f a b c = f (a, b, c)


[<RequireQualifiedAccess>]
module Func5 =
    let curry f a b c d e = f (a, b, c, d, e)