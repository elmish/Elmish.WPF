module UtilsTests.M

open Xunit
open Hedgehog
open Swensen.Unquote

open Elmish.WPF


module refEq =


  [<Fact>]
  let ``returns true if the arguments are referentially equal`` () =
    Property.check <| property {
      let! x = GenX.auto<int * string>
      let y = x
      test <@ refEq x y = true @>
    }


  [<Fact>]
  let ``returns true if the arguments are not referentially equal`` () =
    Property.check <| property {
      let! x = GenX.auto<int * string>
      let! y = GenX.auto<int * string>
      test <@ refEq x y = false @>
    }



module elmEq =


  type TestObj = { X: int }


  module Tuples =


    [<Fact>]
    let ``returns false if any non-string reference type member is not referentially equal`` () =
      PropertyConfig.defaultConfig
      |> PropertyConfig.withTests 1000<tests>
      |> Property.checkWith <| property {
        let! x1 = GenX.auto<int>
        let! y1 = GenX.auto<int>
        let! x2 = GenX.auto<string>
        let! y2 = GenX.auto<string>
        let! x3 = GenX.auto<TestObj>
        let! y3 = GenX.auto<TestObj>
        test <@ elmEq (x1, x2, x3) (y1, y2, y3) = false @>
      }


    [<Fact>]
    let ``returns false if all non-string reference type members are referentially equal and all string and value type members are structurally equal`` () =
      PropertyConfig.defaultConfig
      |> PropertyConfig.withTests 1000<tests>
      |> Property.checkWith <| property {
        let! x1 = GenX.auto<int>
        let! y1 = GenX.auto<int>
        let! x2 = GenX.auto<string>
        let! y2 = GenX.auto<string>
        let! x3 = GenX.auto<TestObj>
        let y3 = x3
        test <@ elmEq (x1, x2, x3) (y1, y2, y3) = (x1 = y1 && x2 = y2) @>
      }


  module Records =


    type TestValues = { i: int; s: string; t: TestObj }


    [<Fact>]
    let ``returns false if any non-string reference type member is not referentially equal`` () =
      PropertyConfig.defaultConfig
      |> PropertyConfig.withTests 1000<tests>
      |> Property.checkWith <| property {
        let! t1 = GenX.auto<TestValues>
        let! t2 = GenX.auto<TestValues>
        test <@ elmEq t1 t2 = false @>
      }


    [<Fact>]
    let ``returns false if all non-string reference type members are referentially equal and all string and value type members are structurally equal`` () =
      PropertyConfig.defaultConfig
      |> PropertyConfig.withTests 1000<tests>
      |> Property.checkWith <| property {
        let! t1 = GenX.auto<TestValues>
        let! t2 = GenX.auto<TestValues>
        let t2 = { t2 with t = t1.t }
        test <@ elmEq t1 t2 = (t1.i = t2.i && t1.s = t2.s) @>
      }


module ValueOption =

  open System

  module toNull =

    let testNonNull (ga: Gen<'a>) =
      Property.check <| property {
        let! expected = ga
        test <@ Ok expected = (expected |> ValueSome |> ValueOption.toNull) @>
      }

    [<Fact>]
    let ``toNull returns contents of ValueSome when given ValueSome`` () =
      testNonNull GenX.auto<obj>
      testNonNull GenX.auto<string>
      testNonNull GenX.auto<int>
      testNonNull GenX.auto<bool>

    let testNullForNullable<'a when 'a : equality> () =
      test <@ Ok Unchecked.defaultof<'a> = ValueOption.toNull<'a> ValueNone @>

    [<Fact>]
    let ``toNull returns null when given ValueNone for nullable type`` () =
      testNullForNullable<obj> ()
      testNullForNullable<string> ()
      testNullForNullable<Nullable<int>> ()
      testNullForNullable<Nullable<bool>> ()

    let testNullForNonNullable<'a when 'a : equality> () =
      let expected = typeof<'a>.Name |> ValueOption.ToNullError.ValueCannotBeNull |> Error
      test <@ expected = ValueOption.toNull<'a> ValueNone @>

    [<Fact>]
    let ``toNull returns ValueCannotBeNull Error when given ValueNone for non-nullable type`` () =
      testNullForNonNullable<int> ()
      testNullForNonNullable<bool> ()


  module ofNull =

    let testNull<'a when 'a : equality> () =
      let input = Unchecked.defaultof<'a>
      test <@ ValueNone = ValueOption.ofNull input @>

    [<Fact>]
    let ``ofNull returns ValueNone when input is null`` () =
      testNull<obj> ()
      testNull<string> ()
      testNull<Nullable<int>> ()

    let testNonNull (ga: Gen<'a>) =
      Property.check <| property {
        let! input = ga
        test <@ ValueSome input = ValueOption.ofNull input @>
      }

    [<Fact>]
    let ``ofNull returns ValueSome of input when input is nonnull`` () =
      testNonNull GenX.auto<obj>
      testNonNull GenX.auto<string>
      testNonNull GenX.auto<int>
