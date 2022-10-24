open Elmish.WPF

open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running

type public BenchmarkDynamicViewModel() =
  let mutable model = 0
  let mutable vm = DynamicViewModel<int, obj>(ViewModelArgs.simple model, [])

  [<GlobalSetup>]
  member public x.GlobalSetup() =
    let createBinding i =
      Binding.oneWay id $"testBinding_%i{i}"

    let bindings =
      System.Linq.Enumerable.Range(0, x.BindingCount) |> Seq.map createBinding |> Seq.toList

    vm <- DynamicViewModel<int, obj>(ViewModelArgs.simple model, bindings)


  [<Benchmark>]
  member public x.Update() =
    model <- 0
    while model < x.UpdateCount do
      model <- model + 1
      IViewModel.updateModel (vm, model)

    vm :> obj

  [<Params (1, 10, 100)>]
  member val public BindingCount = 0 with get, set

  [<Params (1, 100, 10000)>]
  member val public UpdateCount = 0 with get, set


let _ = BenchmarkRunner.Run<BenchmarkDynamicViewModel>()
