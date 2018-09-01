module Elmish.WPF.Tests.TestStub

open Xunit
open Hedgehog
open Swensen.Unquote


[<Fact>]
let ``test stub`` () =
  Property.check <| property {
    test <@ true @>
}
