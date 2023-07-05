module BindingVmHelpersTests.M

open System

open Xunit
open Hedgehog
open Swensen.Unquote

open Elmish.WPF
open Elmish.WPF.BindingVmHelpers



let name = "name"

let noGetSelectedItemCall _ =
  failwith "Should not call get selected item"


module Initialize =

  [<Fact>]
  let ``Initialize doesn't call getCurrentModel`` () =
    let binding =
      BindingData.OneWay.id<string, string>
      |> BindingData.addValidation List.singleton

    let vmBinding =
      Initialize(LoggingViewModelArgs.none, name, noGetSelectedItemCall)
        .Recursive("", ignore, (fun _ -> failwith "Should not call getCurrentModel on initialize"), binding)

    test <@ vmBinding.IsSome @>

module Get =

  let check<'a when 'a: equality> (g: Gen<'a>) =
    Property.check
    <| property {
      let! expectedModel = g

      let binding = BindingData.OneWay.id

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


  [<Fact>]
  let ``should return error on bad typing`` () =
    let binding =
      Binding.SubModel.opt (fun () -> []) >> Binding.mapModel (fun () -> None) <| ""

    let dispatch msg =
      failwith $"Should not dispatch, got {msg}"

    let vmBinding =
      Initialize(LoggingViewModelArgs.none, "Nothing", (fun _ -> failwith "Should not call get selected item"))
        .Recursive((), dispatch, (fun () -> ()), binding.Data)
      |> Option.defaultWith (fun () -> failwith $"Could not create VmBinding after passing in BindingData: {binding}")

    let vmBinding2 = vmBinding |> MapOutputType.unboxVm

    let getResult: Result<int, GetError> = Get("Nothing").Recursive((), vmBinding2)

    test
      <@
        match getResult with
        | Error(GetError.ToNullError(ValueOption.ToNullError.ValueCannotBeNull _)) -> true
        | _ -> false
      @>

module Set =

  let check<'a when 'a: equality> (g: Gen<'a>) =
    Property.check
    <| property {
      let! initialModel = g
      let! newModel = g |> GenX.notEqualTo initialModel

      let model = ref initialModel
      let dispatch msg = model.Value <- msg
      let binding = BindingData.TwoWay.id

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

  let check<'a when 'a: equality> (g: Gen<'a>) =
    Property.check
    <| property {
      let! initialModel = g
      let! newModel = g |> GenX.notEqualTo initialModel

      let model = ref initialModel

      let dispatch msg =
        failwith $"Should not dispatch message {msg}"

      let binding = BindingData.TwoWay.id

      let vmBinding =
        Initialize(LoggingViewModelArgs.none, name, noGetSelectedItemCall)
          .Recursive(initialModel, dispatch, (fun () -> model.Value), binding)
          .Value

      let updateResult =
        Update(LoggingViewModelArgs.none, name)
          .Recursive(initialModel, newModel, vmBinding)

      test <@ updateResult |> List.length = 1 @>
    }

  [<Fact>]
  let ``update successful for various types`` () =
    GenX.auto<float> |> check
    GenX.auto<int32> |> check
    GenX.auto<Guid> |> check
    GenX.auto<string> |> GenX.withNull |> check
    GenX.auto<obj> |> GenX.withNull |> check