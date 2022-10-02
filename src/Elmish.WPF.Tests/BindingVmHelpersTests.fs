module BindingVmHelpersTests.M

open System

open Xunit
open Hedgehog
open Swensen.Unquote

open Elmish.WPF
open Elmish.WPF.BindingVmHelpers



let name = "name"
let noGetSelectedItemCall _ = failwith "Should not call get selected item"


module Get =

  let check<'a when 'a : equality> (g: Gen<'a>) =
    Property.check <| property {
      let! expectedModel = g

      let binding =
        BindingData.OneWay.id
      let vmBinding =
        Initialize(LoggingViewModelArgs.none, name, noGetSelectedItemCall)
          .Recursive(expectedModel, ignore, (fun () -> expectedModel), binding)
          .Value

      let actualModel = Get(name).Recursive(expectedModel, vmBinding)

      test <@ actualModel = Ok expectedModel @>
    }

  [<Fact>]
  let ``get succeeds for various types`` () =
    GenX.auto<float> |> check
    GenX.auto<int32> |> check
    GenX.auto<Guid> |> check
    GenX.auto<string> |> GenX.withNull |> check
    GenX.auto<obj> |> GenX.withNull |> check


module Set =

  let check<'a when 'a : equality> (g: Gen<'a>) =
    Property.check <| property {
      let! initialModel = g
      let! newModel = g |> GenX.notEqualTo initialModel

      let model = ref initialModel
      let dispatch msg = model.Value <- msg
      let binding =
        BindingData.TwoWay.id
      
      let vmBinding =
        Initialize(LoggingViewModelArgs.none, name, noGetSelectedItemCall)
          .Recursive(initialModel, dispatch, (fun () -> model.Value), binding)
          .Value

      test <@ Set(newModel).Recursive(model.Value, vmBinding) @>
      test <@ model.Value = newModel @>
    }

  [<Fact>]
  let ``set successful for various types`` () =
    GenX.auto<float> |> check
    GenX.auto<int32> |> check
    GenX.auto<Guid> |> check
    GenX.auto<string> |> GenX.withNull |> check
    GenX.auto<obj> |> GenX.withNull |> check


module Update =

  let check<'a when 'a : equality> (g: Gen<'a>) =
    Property.check <| property {
      let! initialModel = g
      let! newModel = g |> GenX.notEqualTo initialModel

      let model = ref initialModel
      let dispatch msg = failwith $"Should not dispatch message {msg}"
      let binding =
        BindingData.TwoWay.id
      let vmBinding =
        Initialize(LoggingViewModelArgs.none, name, noGetSelectedItemCall)
          .Recursive(initialModel, dispatch, (fun () -> model.Value), binding)
          .Value

      let updateResult =
        Update(LoggingViewModelArgs.none, name)
          .Recursive(ValueSome initialModel, (fun () -> model.Value), newModel, vmBinding)

      test <@ updateResult |> List.length = 1 @>
    }

  [<Fact>]
  let ``update successful for various types`` () =
    GenX.auto<float> |> check
    GenX.auto<int32> |> check
    GenX.auto<Guid> |> check
    GenX.auto<string> |> GenX.withNull |> check
    GenX.auto<obj> |> GenX.withNull |> check
