module Elmish.WPF.Tests.UtilsTests

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
      Property.check' 1000<tests> <| property {
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
      Property.check' 1000<tests> <| property {
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
      Property.check' 1000<tests> <| property {
        let! t1 = GenX.auto<TestValues>
        let! t2 = GenX.auto<TestValues>
        test <@ elmEq t1 t2 = false @>
      }


    [<Fact>]
    let ``returns false if all non-string reference type members are referentially equal and all string and value type members are structurally equal`` () =
      Property.check' 1000<tests> <| property {
        let! t1 = GenX.auto<TestValues>
        let! t2 = GenX.auto<TestValues>
        let t2 = { t2 with t = t1.t }
        test <@ elmEq t1 t2 = (t1.i = t2.i && t1.s = t2.s) @>
      }
