module BindingVmHelpersTests.M

open System

open Xunit
open Hedgehog
open Swensen.Unquote

open Elmish.WPF
open Elmish.WPF.BindingVmHelpers



module Initialize =

  let checker<'t when 't : equality> (g: Gen<'t>) =
    Property.check <| property {
      let! m1 = g

      let binding = BindingData.OneWay.id

      let vmBinding =
        Initialize(LoggingViewModelArgs.none, "Nothing", (fun _ -> failwith "Should not call get selected item"))
          .Recursive(m1, ignore, (fun () -> m1), binding)

      let result =
        match vmBinding with
        | Some (BaseVmBinding (OneWay b)) -> b
        | b -> failwith $"Expected a OneWay binding, instead found {b}"
      
      test <@ result.OneWayData.Get m1 = m1 @>
    }

  [<Fact>]
  let ``should initialize successfully with various types`` () =
    GenX.auto<float> |> checker
    GenX.auto<int32> |> checker
    GenX.auto<Guid> |> checker
    GenX.auto<string> |> GenX.withNull |> checker
    GenX.auto<obj> |> GenX.withNull |> checker

module Get =

  let checker<'t when 't : equality> (g: Gen<'t>) =
    Property.check <| property {
      let! m1 = g

      let binding = BindingData.OneWay.id
      
      let vmBinding =
        Initialize(LoggingViewModelArgs.none, "Nothing", (fun _ -> failwith "Should not call get selected item"))
          .Recursive(m1, ignore, (fun () -> m1), binding)

      let getResult = vmBinding |> Option.map (fun b -> Get("Nothing").Recursive(m1, b))

      let get =
        match getResult with
        | Some (Ok x) -> x
        | x -> failwith $"Expected a success, instead got {x}"

      test <@ get = m1 @>
    }

  [<Fact>]
  let ``should get successfully with various types`` () =
    GenX.auto<float> |> checker
    GenX.auto<int32> |> checker
    GenX.auto<Guid> |> checker
    GenX.auto<string> |> GenX.withNull |> checker
    GenX.auto<obj> |> GenX.withNull |> checker

module Set =

  let checker<'t when 't : equality> (g: Gen<'t>) =
    Property.check <| property {
      let! m1 = g
      let! m2 = g |> GenX.notEqualTo m1

      let model = ref m1

      let binding = BindingData.TwoWay.id

      let dispatch msg =
        model.Value <- msg
      
      let vmBinding =
        Initialize(LoggingViewModelArgs.none, "Nothing", (fun _ -> failwith "Should not call get selected item"))
          .Recursive(m1, dispatch, (fun () -> model.Value), binding)
        |> Option.defaultWith (fun () -> failwith $"Could not create VmBinding after passing in BindingData: {binding}")

      test <@ Set(m2).Recursive(model.Value, vmBinding) @>
      test <@ model.Value = m2 @>
      test <@ model.Value <> m1 @>
    }

  [<Fact>]
  let ``should set successfully with various types`` () =
    GenX.auto<float> |> checker
    GenX.auto<int32> |> checker
    GenX.auto<Guid> |> checker
    GenX.auto<string> |> GenX.withNull |> checker
    GenX.auto<obj> |> GenX.withNull |> checker

module Update =

  let checker<'t when 't : equality> (g: Gen<'t>) =
    Property.check <| property {
      let! m1 = g
      let! m2 = g |> GenX.notEqualTo m1

      let model = ref m1

      let binding =
        BindingData.TwoWay.id
        |> BindingData.boxT

      let dispatch msg =
        failwith $"Should not dispatch, got {msg}"

      let vmBinding =
        Initialize(LoggingViewModelArgs.none, "Nothing", (fun _ -> failwith "Should not call get selected item"))
          .Recursive(m1, dispatch, (fun () -> model.Value), binding)
        |> Option.defaultWith (fun () -> failwith $"Could not create VmBinding after passing in BindingData: {binding}")

      let updateResult = Update(LoggingViewModelArgs.none, "Nothing").Recursive(ValueSome m1, (fun () -> model.Value), m2, dispatch, vmBinding)

      test <@ updateResult |> List.length = 1 @>
    }

  [<Fact>]
  let ``should update successfully with various types`` () =
    GenX.auto<float> |> checker
    GenX.auto<int32> |> checker
    GenX.auto<Guid> |> checker
    GenX.auto<string> |> GenX.withNull |> checker
    GenX.auto<obj> |> GenX.withNull |> checker
