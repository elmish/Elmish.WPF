module Elmish.WPF.Tests.ViewModelTests

open System
open System.Collections.Concurrent
open System.Collections.ObjectModel
open System.Collections.Specialized
open System.ComponentModel
open System.Windows.Input
open Microsoft.Extensions.Logging.Abstractions
open FSharp.Interop.Dynamic
open Xunit
open Hedgehog
open Swensen.Unquote
open Elmish.WPF


[<AutoOpen>]
module Extensions =

  type ViewModel<'model, 'msg> with

    member internal this.Get propName =
      (?) this propName

    member internal this.Set propName value =
      (?<-) this propName value


type internal TestVm<'model, 'msg>(model, bindings) as this =
  inherit ViewModel<'model, 'msg>({ initialModel = model; dispatch = (fun x -> this.Dispatch x); loggingArgs = LoggingViewModelArgs.fake }, bindings)

  let pcTriggers = ConcurrentDictionary<string, int>()
  let ecTriggers = ConcurrentDictionary<string, int>()
  let ccTriggers = ConcurrentDictionary<string, NotifyCollectionChangedEventArgs list>()
  let cecTriggers = ConcurrentDictionary<string, int>()
  let dispatchMsgs = ResizeArray<'msg> ()


  do
    (this :> INotifyPropertyChanged).PropertyChanged.Add (fun e ->
      pcTriggers.AddOrUpdate(e.PropertyName, 1, (fun _ count -> count + 1)) |> ignore
    )

    (this :> INotifyDataErrorInfo).ErrorsChanged.Add (fun e ->
      ecTriggers.AddOrUpdate(e.PropertyName, 1, (fun _ count -> count + 1)) |> ignore
    )

  new(model, binding) = TestVm(model, [binding])

  member private __.Dispatch x =
    dispatchMsgs.Add x

  member __.NumPcTriggersFor propName =
    pcTriggers.TryGetValue propName |> snd

  member __.NumEcTriggersFor propName =
    ecTriggers.TryGetValue propName |> snd

  member __.NumCcTriggersFor propName =
    ccTriggers.GetOrAdd(propName, []).Length

  member __.NumCecTriggersFor propName =
    cecTriggers.TryGetValue propName |> snd

  member __.Dispatches =
    dispatchMsgs |> Seq.toList

  member __.CcTriggersFor propName =
    ccTriggers.TryGetValue propName |> snd |> Seq.toList

  /// Starts tracking CollectionChanged triggers for the specified prop.
  /// Will cause the property to be retrieved.
  member this.TrackCcTriggersFor propName =
    try
      (this.Get propName : INotifyCollectionChanged).CollectionChanged.Add (fun e ->
        ccTriggers.AddOrUpdate(
          propName,
          [e],
          (fun _ me -> e :: me)) |> ignore
      )
    with _ ->
      (this.Get propName |> unbox<INotifyCollectionChanged>).CollectionChanged.Add (fun e ->
        ccTriggers.AddOrUpdate(
          propName,
          [e],
          (fun _ me -> e :: me)) |> ignore
      )

  /// Starts tracking CanExecuteChanged triggers for the specified prop.
  /// Will cause the property to be retrieved.
  member this.TrackCecTriggersFor propName =
    (this.Get propName : ICommand).CanExecuteChanged.Add (fun _ ->
      cecTriggers.AddOrUpdate(propName, 1, (fun _ count -> count + 1)) |> ignore
    )



[<AutoOpen>]
module Helpers =


  let internal oneWay x = x |> Binding.oneWay
  let internal oneWayLazy x = x |> Func3.curry Binding.oneWayLazy
  let internal oneWaySeqLazy x = x |> Func5.curry Binding.oneWaySeqLazy
  let internal twoWay x = x |> Func2.curry Binding.twoWay
  let internal twoWayValidate
      name
      (get: 'model -> 'a)
      (set: 'a -> 'model -> 'msg)
      (validate: 'model -> string voption) =
    Binding.twoWayValidate (get, set, validate) name


  let internal cmd x = x |> BindingData.Cmd.create



  let internal cmdParam
      name
      (exec: 'a -> 'model -> 'msg voption)
      (canExec: 'a -> 'model -> bool)
      (autoRequery: bool) =
    ({ Exec = unbox >> exec
       CanExec = unbox >> canExec
       AutoRequery = autoRequery }
     |> CmdData
     |> BaseBindingData
     |> createBinding) name


  let internal subModel
      name
      (getModel: 'model -> 'subModel voption)
      (toMsg: 'subMsg -> 'msg)
      (bindings: Binding<'subModel, 'subMsg> list)
      (sticky: bool) =
    Binding.subModelOpt(getModel, snd, toMsg, (fun () -> bindings), sticky) name


  let internal subModelSeq
      name
      (getModels: 'model -> 'subModel list)
      (getId: 'subModel -> 'id)
      (toMsg: 'id * 'subMsg -> 'msg)
      (bindings: Binding<'subModel, 'subMsg> list) =
    name
    |> Binding.subModelSeq (getBindings = (fun () -> bindings), getId = getId)
    |> Binding.mapModel (fun m -> upcast getModels m)
    |> Binding.mapMsg toMsg



  let internal subModelSelectedItem
      name
      subModelSeqBindingName
      (get: 'model -> 'id voption)
      (set: 'id voption -> 'model -> 'msg) =
    Binding.subModelSelectedItem (subModelSeqBindingName, get, set) name



module OneWay =


  [<Fact>]
  let ``when retrieved, should always return the value returned by get`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int>
      let! m2 = GenX.auto<int>

      let get = string<int>

      let binding = oneWay get name
      let vm = TestVm(m1, binding)

      test <@ vm.Get name = get m1 @>

      vm.UpdateModel m2

      test <@ vm.Get name = get m2 @>
  }


  [<Fact>]
  let ``when model is updated, should trigger PC once iff the return value of get changes`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int>
      let! m2 = GenX.auto<int>

      let get = string<int>

      let binding = oneWay get name
      let vm = TestVm(m1, binding)

      vm.UpdateModel m2
      test <@ vm.NumPcTriggersFor name = if get m1 = get m2 then 0 else 1 @>
  }

  [<Fact>]
  let ``on model increment, sticky-to-even binding returns even number`` () =
    let isEven x = x % 2 = 0

    let returnEven a =
      function
      | b when isEven b -> b
      | _ -> a

    Property.check <| property {
      let! name = GenX.auto<string>
      let! m = GenX.auto<int>

      let binding = oneWay id name |> Binding.addSticky isEven
      let vm = TestVm(m, binding)

      vm.UpdateModel (m + 1)
      test <@ vm.Get name = returnEven m (m + 1) @>
    }



module OneWayLazy =


  [<Fact>]
  let ``when retrieved initially, should return the value returned by map`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m = GenX.auto<int>

      let get = string<int>
      let equals = (=)
      let map = String.length

      let binding = oneWayLazy get equals map name
      let vm = TestVm(m, binding)

      test <@ vm.Get name = (m |> get |> map) @>
  }


  [<Fact>]
  let ``when retrieved after update and equals returns false, should return the value returned by map`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int>
      let! m2 = GenX.auto<int>

      let get = string<int>
      let equals _ _ = false
      let map = String.length

      let binding = oneWayLazy get equals map name
      let vm = TestVm(m1, binding)
      vm.UpdateModel m2

      test <@ vm.Get name = (m2 |> get |> map) @>
  }


  [<Fact>]
  let ``when retrieved after update and equals returns true, should return the previous value returned by map`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int>
      let! m2 = GenX.auto<int>

      let get = string<int>
      let equals _ _ = true
      let map = String.length

      let binding = oneWayLazy get equals map name
      let vm = TestVm(m1, binding)
      vm.Get name |> ignore  // populate cache
      vm.UpdateModel m2

      test <@ vm.Get name = (m1 |> get |> map) @>
  }


  [<Fact>]
  let ``when retrieved, updated, and retrieved again, should call map once after the update iff equals returns false`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int>
      let! m2 = GenX.auto<int>
      let! eq = Gen.bool

      let get = string
      let equals _ _ = eq
      let map = InvokeTester String.length

      let binding = oneWayLazy get equals map.Fn name
      let vm = TestVm(m1, binding)

      vm.Get name |> ignore
      vm.UpdateModel m2
      map.Reset ()
      vm.Get name |> ignore

      test <@ map.Count = if eq then 0 else 1 @>
  }


  [<Fact>]
  let ``map should never be called during model update`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int>
      let! m2 = GenX.auto<int>

      let get = string
      let equals = (=)
      let map = InvokeTester String.length

      let binding = oneWayLazy get equals map.Fn name
      let vm = TestVm(m1, binding)

      vm.UpdateModel m2

      test <@ map.Count = 0 @>
  }


  [<Fact>]
  let ``when retrieved several times between updates, map is called at most once`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int>
      let! m2 = GenX.auto<int>

      let get = string
      let equals = (=)
      let map = InvokeTester String.length

      let binding = oneWayLazy get equals map.Fn name
      let vm = TestVm(m1, binding)

      vm.Get name |> ignore
      vm.Get name |> ignore
      test <@ map.Count <= 1 @>

      map.Reset ()
      vm.UpdateModel m2
      vm.Get name |> ignore
      vm.Get name |> ignore
      test <@ map.Count <= 1 @>
    }


  [<Fact>]
  let ``when model is updated, should trigger PC once iff equals is false`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int>
      let! m2 = GenX.auto<int>
      let! eq = Gen.bool

      let get = string
      let equals _ _ = eq
      let map = String.length

      let binding = oneWayLazy get equals map name
      let vm = TestVm(m1, binding)
      vm.UpdateModel m2

      test <@ vm.NumPcTriggersFor name = if not eq then 1 else 0 @>
  }


module OneWaySeqLazy =


  let private testObservableCollectionContainsExpectedItems (vm: ViewModel<_, _>) name expected =
    let actual = (vm.Get name : ObservableCollection<_>) |> Seq.toList
    test <@ expected = actual @>


  [<Fact>]
  let ``when retrieved initially, should return an ObservableCollection with the values returned by map`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m = GenX.auto<Guid list>

      let get = id
      let equals = (=)
      let map = id
      let itemEquals = (=)
      let getId = id

      let binding = oneWaySeqLazy get equals map itemEquals getId name
      let vm = TestVm(m, binding)

      testObservableCollectionContainsExpectedItems vm name (m |> get |> map)
    }


  [<Fact>]
  let ``given equals returns false, when retrieved after update, should return an ObservableCollection with the new values returned by map`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<Guid list>
      let! m2 = GenX.auto<Guid list>

      let get = id
      let equals _ _ = false
      let map = id
      let itemEquals = (=)
      let getId = id

      let binding = oneWaySeqLazy get equals map itemEquals getId name
      let vm = TestVm(m1, binding)

      vm.UpdateModel m2

      testObservableCollectionContainsExpectedItems vm name (m2 |> get |> map)
    }


  [<Fact>]
  let ``given equals returns true, when retrieved after update, should return an ObservableCollection with the previous values returned by map`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<Guid list>
      let! m2 = GenX.auto<Guid list>

      let get = id
      let equals _ _ = true
      let map = id
      let itemEquals = (=)
      let getId = id

      let binding = oneWaySeqLazy get equals map itemEquals getId name
      let vm = TestVm(m1, binding)

      vm.UpdateModel m2

      testObservableCollectionContainsExpectedItems vm name (m1 |> get |> map)
    }


  [<Fact>]
  let ``during VM instantiation, get should be called at most once`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<Guid list>
      let! eq = Gen.bool

      let get = InvokeTester id
      let equals _ _ = eq
      let map = id
      let itemEquals = (=)
      let getId = id

      let binding = oneWaySeqLazy get.Fn equals map itemEquals getId name
      TestVm(m1, binding) |> ignore

      test <@ get.Count <= 1 @>
    }


  [<Fact>]
  let ``during VM instantiation, map should have be called at most once`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<Guid list>
      let! eq = Gen.bool

      let get = id
      let equals _ _ = eq
      let map = InvokeTester id
      let itemEquals = (=)
      let getId = id

      let binding = oneWaySeqLazy get equals map.Fn itemEquals getId name
      TestVm(m1, binding) |> ignore

      test <@ map.Count <= 1 @>
    }


  [<Fact>]
  let ``given equals returns true, during model update, map should be called at most once`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<Guid list>
      let! m2 = GenX.auto<Guid list>

      let get = id
      let equals _ _ = true
      let map = InvokeTester id
      let itemEquals = (=)
      let getId = id

      let binding = oneWaySeqLazy get equals map.Fn itemEquals getId name
      let vm = TestVm(m1, binding)

      map.Reset ()
      vm.UpdateModel m2

      test <@ map.Count = 0 @>
    }


  [<Fact>]
  let ``when equals returns false, during model update, map should be called at most once`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<Guid list>
      let! m2 = GenX.auto<Guid list>

      let get = id
      let equals _ _ = false
      let map = InvokeTester id
      let itemEquals = (=)
      let getId = id

      let binding = oneWaySeqLazy get equals map.Fn itemEquals getId name
      let vm = TestVm(m1, binding)

      map.Reset ()
      vm.UpdateModel m2

      test <@ map.Count <= 1 @>
    }


  [<Fact>]
  let ``during model update, get should be called at most twice`` () = // once on current model and once on new model
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<Guid list>
      let! m2 = GenX.auto<Guid list>
      let! eq = Gen.bool

      let get = InvokeTester id
      let equals _ _ = eq
      let map = id
      let itemEquals = (=)
      let getId = id

      let binding = oneWaySeqLazy get.Fn equals map itemEquals getId name
      let vm = TestVm(m1, binding)

      get.Reset ()
      vm.UpdateModel m2

      test <@ get.Count <= 2 @>
    }


  [<Fact>]
  let ``when retrieved several times after VM initialization, map is called at most once`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<Guid list>

      let get = id
      let equals = (=)
      let map = InvokeTester id
      let itemEquals = (=)
      let getId = id

      let binding = oneWaySeqLazy get equals map.Fn itemEquals getId name
      let vm = TestVm(m1, binding)

      vm.Get name |> ignore
      vm.Get name |> ignore

      test <@ map.Count <= 1 @>
    }


  [<Fact>]
  let ``when retrieved several times after update, map is called at most once`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<Guid list>
      let! m2 = GenX.auto<Guid list>

      let get = id
      let equals = (=)
      let map = InvokeTester id
      let itemEquals = (=)
      let getId = id

      let binding = oneWaySeqLazy get equals map.Fn itemEquals getId name
      let vm = TestVm(m1, binding)

      map.Reset ()
      vm.UpdateModel m2

      vm.Get name |> ignore
      vm.Get name |> ignore

      test <@ map.Count <= 1 @>
    }


  [<Fact>]
  let ``for any behavior of equals or itemEquals, when model is updated, should never trigger PC`` () =  // because this binding should only trigger CC
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<Guid list>
      let! m2 = GenX.auto<Guid list>
      let! eq = Gen.bool
      let! itemEq = Gen.bool

      let get = id
      let equals _ _ = eq
      let map = InvokeTester id
      let itemEquals _ _ = itemEq
      let getId = id

      let binding = oneWaySeqLazy get equals map.Fn itemEquals getId name
      let vm = TestVm(m1, binding)

      vm.UpdateModel m2

      test <@ vm.NumPcTriggersFor name = 0 @>
    }


  [<Fact>]
  let ``given equals returns true, when model is updated, should never trigger CC`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<Guid list>
      let! m2 = GenX.auto<Guid list>

      let get = id
      let equals _ _ = true
      let map = id
      let itemEquals = (=)
      let getId = id

      let binding = oneWaySeqLazy get equals map itemEquals getId name
      let vm = TestVm(m1, binding)

      vm.TrackCcTriggersFor name
      vm.UpdateModel m2

      test <@ vm.NumCcTriggersFor name = 0 @>
    }


  [<Fact>]
  let ``given equals returns false and itemEquals returns false, when model is updated, should contain expected items in collection`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = Gen.guid |> Gen.list (Range.constant 1 50)
      let! m2 = Gen.guid |> Gen.list (Range.constant 1 50)

      let get = id
      let equals _ _ = false
      let map = id
      let itemEquals _ _ = false
      let getId = id

      let binding = oneWaySeqLazy get equals map itemEquals getId name
      let vm = TestVm(m1, binding)

      vm.UpdateModel m2

      testObservableCollectionContainsExpectedItems vm name m2
    }



module TwoWay =


  [<Fact>]
  let ``when retrieved, should always return the value returned by get`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int>
      let! m2 = GenX.auto<int>

      let get = string<int>
      let set _ _ = ()

      let binding = twoWay get set name
      let vm = TestVm(m1, binding)

      test <@ vm.Get name = get m1 @>

      vm.UpdateModel m2

      test <@ vm.Get name = get m2 @>
  }


  [<Fact>]
  let ``when model is updated, should trigger PC once iff the return value of get changes`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int>
      let! m2 = GenX.auto<int>

      let get = string<int>
      let set _ _ = ()

      let binding = twoWay get set name
      let vm = TestVm(m1, binding)

      vm.UpdateModel m2
      test <@ vm.NumPcTriggersFor name = if get m1 = get m2 then 0 else 1 @>
  }


  [<Fact>]
  let ``when set, should call dispatch once with the value returned by set`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m = GenX.auto<int>
      let! p = GenX.auto<string>

      let get = string
      let set (p: string) (m: int) = string m + p

      let binding = twoWay get set name
      let vm = TestVm(m, binding)

      vm.Set name p

      test <@ vm.Dispatches = [set p m] @>
    }



module TwoWayValidate =


  [<Fact>]
  let ``when retrieved, should always return the value returned by get`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int>
      let! m2 = GenX.auto<int>

      let get = string<int>
      let set _ _ = ()
      let validate _ = ValueNone

      let binding = twoWayValidate name get set validate
      let vm = TestVm(m1, binding)

      test <@ vm.Get name = get m1 @>

      vm.UpdateModel m2

      test <@ vm.Get name = get m2 @>
  }


  [<Fact>]
  let ``when model is updated, should trigger PC once iff the return value of get changes`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int>
      let! m2 = GenX.auto<int>

      let get = string<int>
      let set _ _ = ()
      let validate _ = ValueNone

      let binding = twoWayValidate name get set validate
      let vm = TestVm(m1, binding)

      vm.UpdateModel m2

      test <@ vm.NumPcTriggersFor name = if get m1 = get m2 then 0 else 1 @>
  }


  [<Fact>]
  let ``when set, should call dispatch once with the value returned by set`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m = GenX.auto<int>
      let! p = GenX.auto<string>

      let get = string
      let set (p: string) (m: int) = string m + p
      let validate _ = ValueNone

      let binding = twoWayValidate name get set validate
      let vm = TestVm(m, binding)

      vm.Set name p

      test <@ vm.Dispatches = [set p m] @>
    }


  [<Fact>]
  let ``when model is updated, should trigger ErrorsChanged iff the value returned by validate changes`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int>
      let! m2 = GenX.auto<int>

      let get _ = ()
      let set _ _ = ()
      let validate m = if m < 0 then ValueSome (string m) else ValueNone

      let binding = twoWayValidate name get set validate
      let vm = TestVm(m1, binding)

      vm.UpdateModel m2

      test <@ vm.NumEcTriggersFor name = if validate m1 = validate m2 then 0 else 1 @>
    }


  [<Fact>]
  let ``when validate returns ValueNone, HasErrors should return false and GetErrors should return an empty collection`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int>
      let! m2 = GenX.auto<int>

      let get _ = ()
      let set _ _ = ()
      let validate _ = ValueNone

      let binding = twoWayValidate name get set validate
      let vm = TestVm(m1, binding)
      let vm' = vm :> INotifyDataErrorInfo

      test <@ vm'.HasErrors = false @>
      test <@ vm'.GetErrors name |> Seq.cast |> Seq.isEmpty @>

      vm.UpdateModel m2

      test <@ vm'.HasErrors = false @>
      test <@ vm'.GetErrors name |> Seq.cast |> Seq.isEmpty @>
    }


  [<Fact>]
  let ``when validate returns ValueSome, HasErrors should return true and GetErrors should return a collection with a single element equal to the inner value returned by validate`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int>
      let! m2 = GenX.auto<int>

      let get _ = ()
      let set _ _ = ()
      let validate m = ValueSome (string<int> m)

      let binding = twoWayValidate name get set validate
      let vm = TestVm(m1, binding)
      let vm' = vm :> INotifyDataErrorInfo

      test <@ vm'.HasErrors = true @>
      test <@ vm'.GetErrors name |> Seq.cast |> Seq.toList = [(validate m1).Value] @>

      vm.UpdateModel m2

      test <@ vm'.HasErrors = true @>
      test <@ vm'.GetErrors name |> Seq.cast |> Seq.toList = [(validate m2).Value] @>
    }


  [<Fact>]
  let ``when validate returns no ValueNone after returning ValueSome, HasErrors should return false and GetErrors should return an empty collection`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int>
      let! m2 = GenX.auto<int> |> GenX.notEqualTo m1

      let get _ = ()
      let set _ _ = ()
      let validate m =
        if m = m1
        then ValueSome (string<int> m)
        else ValueNone

      let binding = twoWayValidate name get set validate
      let vm = TestVm(m1, binding)
      let vm' = vm :> INotifyDataErrorInfo

      test <@ vm'.HasErrors = true @>
      test <@ vm'.GetErrors name |> Seq.cast |> Seq.toList = [(validate m1).Value] @>

      vm.UpdateModel m2

      test <@ vm'.HasErrors = false @>
      test <@ vm'.GetErrors name |> Seq.cast |> Seq.isEmpty @>
    }



module Cmd =


  [<Fact>]
  let ``the retrieved command's Execute should call dispatch once with the inner value returned by exec`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m = GenX.auto<int>
      let! p = GenX.auto<obj> |> GenX.withNull

      let exec m = if m < 0 then ValueNone else ValueSome (string m)
      let canExec m = m < 0

      let binding = cmd exec canExec name
      let vm = TestVm(m, binding)

      (vm.Get name : ICommand).Execute(p)

      match exec m with
      | ValueSome msg -> test <@ vm.Dispatches = [msg] @>
      | ValueNone -> test <@ vm.Dispatches = [] @>
    }


  [<Fact>]
  let ``the retrieved command's CanExecute should return the value returned by canExec`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m = GenX.auto<int>
      let! p = GenX.auto<obj> |> GenX.withNull

      let exec m = if m < 0 then ValueNone else ValueSome (string m)
      let canExec m = m < 0

      let binding = cmd exec canExec name
      let vm = TestVm(m, binding)

      test <@ (vm.Get name : ICommand).CanExecute(p) = canExec m @>
    }


  [<Fact>]
  let ``when model is updated, should trigger CanExecuteChanged iff the output of canExec changes`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int>
      let! m2 = GenX.auto<int>

      let exec m = if m < 0 then ValueNone else ValueSome (string m)
      let canExec m = m < 0

      let binding = cmd exec canExec name
      let vm = TestVm(m1, binding)

      vm.TrackCecTriggersFor name
      vm.UpdateModel m2

      test <@ vm.NumCecTriggersFor name = if canExec m1 = canExec m2 then 0 else 1 @>
    }


  [<Fact>]
  let ``when model is updated, should never trigger PC`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int>
      let! m2 = GenX.auto<int>

      let exec m = if m < 0 then ValueNone else ValueSome (string m)
      let canExec m = m < 0

      let binding = cmd exec canExec name
      let vm = TestVm(m1, binding)

      vm.UpdateModel m2

      test <@ vm.NumPcTriggersFor name = 0 @>
    }



module CmdParam =


  [<Fact>]
  let ``the retrieved command's Execute should call dispatch once with the inner value returned by exec`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m = GenX.auto<int>
      let! p = GenX.auto<string>
      let! autoRequery = Gen.bool

      let exec (p: string) m = if p.Length + m < 0 then ValueNone else ValueSome (string m + p)
      let canExec (p: string) m = p.Length + m < 0

      let binding = cmdParam name exec canExec autoRequery
      let vm = TestVm(m, binding)

      (vm.Get name : ICommand).Execute(p)

      match exec p m with
      | ValueSome msg -> test <@ vm.Dispatches = [msg] @>
      | ValueNone -> test <@ vm.Dispatches = [] @>
    }


  [<Fact>]
  let ``the retrieved command's CanExecute should return the value returned by canExec`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m = GenX.auto<int>
      let! p = GenX.auto<string>
      let! autoRequery = Gen.bool

      let exec (p: string) m = if p.Length + m < 0 then ValueNone else ValueSome (string m + p)
      let canExec (p: string) m = p.Length + m < 0

      let binding = cmdParam name exec canExec autoRequery
      let vm = TestVm(m, binding)

      test <@ (vm.Get name : ICommand).CanExecute(p) = canExec p m @>
    }


  [<Fact>]
  let ``when model is updated, should always trigger CanExecuteChanged`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int>
      let! m2 = GenX.auto<int>
      let! autoRequery = Gen.bool

      let exec (p: string) m = if p.Length + m < 0 then ValueNone else ValueSome (string m + p)
      let canExec (p: string) m = p.Length + m < 0

      let binding = cmdParam name exec canExec autoRequery
      let vm = TestVm(m1, binding)

      vm.TrackCecTriggersFor name
      vm.UpdateModel m2

      test <@ vm.NumCecTriggersFor name = 1 @>
    }


  [<Fact>]
  let ``when model is updated, should never trigger PC`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int>
      let! m2 = GenX.auto<int>
      let! autoRequery = Gen.bool

      let exec (p: string) m = if p.Length + m < 0 then ValueNone else ValueSome (string m + p)
      let canExec (p: string) m = p.Length + m < 0

      let binding = cmdParam name exec canExec autoRequery
      let vm = TestVm(m1, binding)

      vm.UpdateModel m2

      test <@ vm.NumPcTriggersFor name = 0 @>
    }



module SubModel =


  [<Fact>]
  let ``when retrieved and getModel returns ValueSome, should return a ViewModel whose CurrentModel is the inner value returned by getModel`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<byte * int>
      let! m2 = GenX.auto<byte * int>
      let! sticky = Gen.bool

      let getModel (m: byte * int) = (snd m) / 2 |> ValueSome
      let toMsg _ = ()

      let binding = subModel name getModel toMsg [] sticky
      let vm = TestVm(m1, binding)

      test <@ (vm.Get name : ViewModel<int, obj>).CurrentModel = (getModel m1).Value @>

      vm.UpdateModel m2

      test <@ (vm.Get name : ViewModel<int, obj>).CurrentModel = (getModel m2).Value @>
    }


  [<Fact>]
  let ``when retrieved initially and getModel returns ValueNone, should return null`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m = GenX.auto<byte * int>
      let! sticky = Gen.bool

      let getModel _ = ValueNone
      let toMsg _ = ()

      let binding = subModel name getModel toMsg [] sticky
      let vm = TestVm(m, binding)

      test <@ vm.Get name |> isNull @>
    }


  [<Fact>]
  let ``when retrieved after update and getModel changes between ValueSome and ValueNone, should return null if sticky is false, otherwise the last non-null value`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<byte * int>
      let! m2 = GenX.auto<byte * int> |> GenX.notEqualTo m1
      let! m3 = GenX.auto<byte * int> |> GenX.notEqualTo m1  |> GenX.notEqualTo m2
      let! sticky = Gen.bool

      let getModel (m: byte * int) =
        if m = m1 then (snd m) / 2 |> ValueSome
        elif m = m2 then ValueNone
        elif m = m3 then (snd m) / 3 |> ValueSome
        else failwith "Should never happen"
      let toMsg _ = ()

      let binding = subModel name getModel toMsg [] sticky
      let vm = TestVm(m1, binding)

      test <@ (vm.Get name : ViewModel<int, obj>).CurrentModel = (getModel m1).Value @>

      vm.UpdateModel m2

      if sticky then
        test <@ (vm.Get name : ViewModel<int, obj>).CurrentModel = (getModel m1).Value @>
      else
        test <@ vm.Get name |> isNull @>

      vm.UpdateModel m3

      test <@ (vm.Get name : ViewModel<int, obj>).CurrentModel = (getModel m3).Value @>
    }


  [<Fact>]
  let ``when model is updated, should trigger PC once iff getModel changes from ValueNone to ValueSome, or from ValueSome to ValueNone when sticky is false`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<byte * int>
      let! m2 = GenX.auto<byte * int> |> GenX.notEqualTo m1
      let! sticky = Gen.bool

      let getModel (m: byte * int) = if snd m < 0 then ValueNone else (snd m) / 2 |> ValueSome
      let toMsg _ = ()

      let binding = subModel name getModel toMsg [] sticky
      let vm = TestVm(m1, binding)

      vm.UpdateModel m2

      let wasSome = (getModel m1).IsSome
      let wasNone = (getModel m1).IsNone
      let isSome = (getModel m2).IsSome
      let isNone = (getModel m2).IsNone
      test <@ vm.NumPcTriggersFor name =
                if wasNone && isSome then 1
                elif wasSome && isNone && not sticky then 1
                else 0 @>
    }


  [<Fact>]
  let ``smoke test: when a sub-model OneWay binding is retrieved, returns the value returned by get`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! subName = GenX.auto<string>
      let! m = GenX.auto<byte * int>
      let! sticky = Gen.bool

      let getModel = snd >> ValueSome
      let toMsg _ = ()
      let subGet = string<int>

      let subBinding = oneWay subGet subName
      let binding = subModel name getModel toMsg [subBinding] sticky
      let vm = TestVm(m, binding)

      test <@ (vm.Get name : ViewModel<int,obj>).Get subName = ((getModel m).Value |> subGet) @>
    }


  [<Fact>]
  let ``smoke test: when a sub-model TwoWay binding is set, dispatches the value returned by set transformed by toMsg`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! subName = GenX.auto<string>
      let! m = GenX.auto<byte * int>
      let! p = GenX.auto<string>
      let! sticky = Gen.bool

      let getModel : byte * int -> int voption = snd >> ValueSome
      let toMsg = String.length
      let subGet : int -> string = string
      let subSet (p: string) (m: int) = p + string m

      let subBinding = twoWay subGet subSet subName
      let binding = subModel name getModel toMsg [subBinding] sticky
      let vm = TestVm(m, binding)

      (vm.Get name : ViewModel<int,string>).Set subName p

      test <@ vm.Dispatches = [subSet p (getModel m).Value |> toMsg] @>
    }



module SubModelSeq =

  let private testObservableCollectionContainsExpectedItems (vm: ViewModel<Guid list, (Guid * obj)>) name expected =
    let actual =
      vm.Get name
      |> unbox<ObservableCollection<ViewModel<Guid,obj>>>
      |> Seq.map (fun vm -> vm.CurrentModel)
      |> Seq.toList
    test <@ expected = actual @>

  [<Fact>]
  let ``when retrieved, should return an ObservableCollection with ViewModels whose CurrentModel is the corresponding value returned by getModels`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m = GenX.auto<Guid list>

      let getModels = id
      let getId = id
      let toMsg = id

      let binding = subModelSeq name getModels getId toMsg []
      let vm = TestVm(m, binding)

      testObservableCollectionContainsExpectedItems vm name m
    }


  [<Fact>]
  let ``when model is updated, should never trigger PC`` () =  // because this binding should only trigger CC
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<Guid list>
      let! m2 = GenX.auto<Guid list>

      let getModels = id
      let getId = id
      let toMsg = id

      let binding = subModelSeq name getModels getId toMsg []
      let vm = TestVm(m1, binding)

      vm.UpdateModel m2

      test <@ vm.NumPcTriggersFor name = 0 @>
    }


  [<Fact>]
  let ``given elements are the same, when model is updated, should not trigger CC`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m = GenX.auto<Guid list>

      let getModels = id
      let getId = id
      let toMsg = id

      let binding = subModelSeq name getModels getId toMsg []
      let vm = TestVm(m, binding)

      vm.TrackCcTriggersFor name

      vm.UpdateModel m

      test <@ vm.NumCcTriggersFor name = 0 @>
    }


  [<Fact>]
  let ``smoke test: when a sub-model OneWay binding is retrieved, returns the value returned by get`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! subName = GenX.auto<string>
      let! m = GenX.auto<Guid list>

      let getModels = id
      let getId = id
      let toMsg = id
      let subGet = string

      let subBinding = oneWay subGet subName
      let binding = subModelSeq name getModels getId toMsg [subBinding]
      let vm = TestVm(m, binding)

      let actual =
        vm.Get name
        |> unbox<ObservableCollection<ViewModel<Guid,obj>>>
        |> Seq.map (fun vm -> vm.Get subName |> unbox<string>)
        |> Seq.toList

      let expected = getModels m |> Seq.map subGet |> Seq.toList
      test <@ expected = actual @>
    }


  [<Fact>]
  let ``smoke test: when a sub-model TwoWay binding is set, dispatches the value returned by set transformed by toMsg`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! subName = GenX.auto<string>
      let! m = GenX.auto<Guid list>
      let! p = GenX.auto<string>

      let getModels = id
      let getId = string
      let toMsg (id: string, subMsg: string) = (id + subMsg).Length
      let subGet = string
      let subSet (p: string) (m: Guid) = p + string m

      let subBinding = twoWay subGet subSet subName
      let binding = subModelSeq name getModels getId toMsg [subBinding]
      let vm = TestVm(m, binding)

      vm.Get name
      |> unbox<ObservableCollection<ViewModel<Guid,string>>>
      |> Seq.iter (fun vm -> vm.Set subName p)

      let expected = m |> getModels |> List.map (fun m -> (getId m, subSet p m) |> toMsg)
      test <@ expected = vm.Dispatches @>
    }



module SubModelSelectedItem =


  [<Fact>]
  let ``Should return the VM corresponding to the ID that has been set`` () =
    Property.check <| property {
      let! subModelSeqName = GenX.auto<string>
      let! selectedItemName = GenX.auto<string> |> GenX.notEqualTo subModelSeqName
      let! m = GenX.auto<int * Guid list>
      let! selectedSubModel =
        match snd m with
        | [] -> Gen.constant ValueNone
        | xs -> Gen.item xs |> Gen.map ValueSome

      let getModels : int * Guid list -> Guid list = snd
      let getId : Guid -> string = string
      let toMsg = snd

      let get _ = selectedSubModel |> ValueOption.map getId
      let set _ _ = ()

      let subModelSeqBinding = subModelSeq subModelSeqName getModels getId toMsg []
      let selectedItemBinding = subModelSelectedItem selectedItemName subModelSeqName get set

      let vm = TestVm(m, [subModelSeqBinding; selectedItemBinding])

      match selectedSubModel with
      | ValueNone ->
          test <@ vm.Get selectedItemName = null @>
      | ValueSome sm ->
          test <@ (vm.Get selectedItemName |> unbox<ViewModel<Guid,unit>>) |> Option.ofObj |> Option.map (fun vm -> vm.CurrentModel)
                   = (m |> getModels |> List.tryFind (fun x -> getId x = getId sm))
               @>
    }


  [<Fact>]
  let ``when set, should dispatch the message returned by set`` () =
    Property.check <| property {
      let! subModelSeqName = GenX.auto<string>
      let! selectedItemName = GenX.auto<string> |> GenX.notEqualTo subModelSeqName
      let! m = GenX.auto<int * Guid list>
      let! selectedSubModel =
        match snd m with
        | [] -> Gen.constant ValueNone
        | xs -> Gen.item xs |> Gen.map ValueSome

      let getModels : int * Guid list -> Guid list = snd
      let getId : Guid -> string = string
      let toMsg = snd

      let get _ = selectedSubModel |> ValueOption.map getId
      let set (p: string voption) (m: int * Guid list) =
        p |> ValueOption.map (String.length >> (+) (fst m))

      let subModelSeqBinding = subModelSeq subModelSeqName getModels getId toMsg []
      let selectedItemBinding = subModelSelectedItem selectedItemName subModelSeqName get set

      let vm = TestVm(m, [subModelSeqBinding; selectedItemBinding])

      let selectedVm =
        selectedSubModel |> ValueOption.bind (fun sm ->
          vm.Get subModelSeqName
          |> unbox<ObservableCollection<ViewModel<Guid,int voption>>>
          |> Seq.tryFind (fun vm -> vm.CurrentModel |> getId = getId sm)
          |> ValueOption.ofOption
        )
        |> ValueOption.toObj

      vm.Set selectedItemName selectedVm

      test <@ vm.Dispatches = [ set (selectedSubModel |> ValueOption.map getId) m ] @>
    }



module AlterMsgStream =

  [<Fact>]
  let ``message stream alteration only invoked once when set called twice`` () =
    let name = ""
    let model = 0
    let get = ignore
    let set _ _ = ()
    let alteration = InvokeTester id
    let binding =
      twoWay get set ""
      |> Binding.alterMsgStream alteration.Fn
    let vm = TestVm(model, binding)

    vm.Set name ()
    vm.Set name ()

    test <@ 1 = alteration.Count @>
