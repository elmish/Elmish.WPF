namespace Elmish.WPF.Tests.ViewModelTests

open System
open System.Collections.Concurrent
open System.Collections.ObjectModel
open System.Collections.Specialized
open System.ComponentModel
open System.Windows.Input
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
  inherit ViewModel<'model, 'msg>(model, (fun x -> this.Dispatch x), bindings, ElmConfig.Default, "")

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

  member private _.Dispatch x =
    dispatchMsgs.Add x

  member _.NumPcTriggersFor propName =
    pcTriggers.TryGetValue propName |> snd

  member _.NumEcTriggersFor propName =
    ecTriggers.TryGetValue propName |> snd

  member _.NumCcTriggersFor propName =
    ccTriggers.GetOrAdd(propName, []).Length

  member _.NumCecTriggersFor propName =
    cecTriggers.TryGetValue propName |> snd

  member _.Dispatches =
    dispatchMsgs |> Seq.toList

  member _.CcTriggersFor propName =
    ccTriggers.TryGetValue propName |> snd |> Seq.toList

  /// Starts tracking CollectionChanged triggers for the specified prop.
  /// Will cause the property to be retrieved.
  member this.TrackCcTriggersFor propName =
    try
      (this.Get propName : ObservableCollection<obj>).CollectionChanged.Add (fun e ->
        ccTriggers.AddOrUpdate(
          propName,
          [e],
          (fun _ me -> e :: me)) |> ignore
      )
    with _ ->
      (this.Get propName |> unbox<ObservableCollection<ViewModel<obj, obj>>>).CollectionChanged.Add (fun e ->
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


type InvokeTesterVal<'a, 'b>(initialRet: 'b) =
  let mutable count = 0
  let mutable values = []
  let mutable retVal = initialRet
  let wrapped x =
    count <- count + 1
    values <- values @ [x]
    retVal
  member _.Fn = wrapped
  member _.Count = count
  member _.Values = values
  member _.SetRetVal ret = retVal <- ret
  member _.Reset () =
    count <- 0
    values <- []
    retVal <- initialRet


type InvokeTesterVal2<'a, 'b, 'c>(initialRet: 'c) =
  let mutable count = 0
  let mutable values = []
  let mutable retVal = initialRet
  let wrapped x y =
    count <- count + 1
    values <- values @ [(x, y)]
    retVal
  member _.Fn : 'a -> 'b -> 'c = wrapped
  member _.Count = count
  member _.Values = values
  member _.SetRetVal ret = retVal <- ret
  member _.Reset () =
    count <- 0
    values <- []
    retVal <- initialRet


type InvokeTester<'a, 'b>(f: 'a -> 'b) =
  let mutable count = 0
  let mutable values = []
  let wrapped x =
    count <- count + 1
    values <- values @ [x]
    f x
  member _.Fn = wrapped
  member _.Count = count
  member _.Values = values
  member _.Reset () =
    count <- 0
    values <- []


type InvokeTester2<'a, 'b, 'c>(f: 'a -> 'b -> 'c) =
  let mutable count = 0
  let mutable values = []
  let wrapped x y =
    count <- count + 1
    values <- values @ [x, y]
    f x
  member _.Fn = wrapped
  member _.Count = count
  member _.Values = values
  member _.Reset () =
    count <- 0
    values <- []



[<AutoOpen>]
module Helpers =


  module String =

    let length (s: string) = s.Length


  let internal oneWay
      name
      (get: 'model -> 'a) =
    name |> createBinding (OneWayData {
      Get = get >> box
    })


  let internal oneWayLazy
      name
      (get: 'model -> 'a)
      (equals: 'a -> 'a -> bool)
      (map: 'a -> 'b) =
    name |> createBinding (OneWayLazyData {
      Get = get >> box
      Map = unbox<'a> >> map >> box
      Equals = fun a b -> equals (unbox<'a> a) (unbox<'a> b)
    })


  let internal oneWaySeqLazy
      name
      (get: 'model -> 'a)
      (equals: 'a -> 'a -> bool)
      (map: 'a -> #seq<'b>)
      (itemEquals: 'b -> 'b -> bool)
      (getId: 'b -> 'id) =
    name |> createBinding (OneWaySeqLazyData {
      Get = get >> box
      Map = unbox<'a> >> map >> Seq.map box
      Equals = fun x y -> equals (unbox<'a> x) (unbox<'a> y)
      GetId = unbox<'b> >> getId >> box
      ItemEquals = fun x y -> itemEquals (unbox<'b> x) (unbox<'b> y)
    })


  let internal twoWay
      name
      (get: 'model -> 'a)
      (set: 'a -> 'model -> 'msg) =
    name |> createBinding (TwoWayData {
      Get = get >> box
      Set = unbox<'a> >> set
      WrapDispatch = id
    })


  let internal twoWayValidate
      name
      (get: 'model -> 'a)
      (set: 'a -> 'model -> 'msg)
      (validate: 'model -> string voption) =
    name |> createBinding (TwoWayValidateData {
      Get = get >> box
      Set = unbox<'a> >> set
      Validate = validate
      WrapDispatch = id
    })


  let internal cmd
      name
      (exec: 'model -> 'msg voption)
      (canExec: 'model -> bool) =
    name |> createBinding (CmdData {
      Exec = exec
      CanExec = canExec
      WrapDispatch = id
    })


  let internal cmdParam
      name
      (exec: 'a -> 'model -> 'msg voption)
      (canExec: 'a -> 'model -> bool)
      (autoRequery: bool) =
    name |> createBinding (CmdParamData {
      Exec = unbox >> exec
      CanExec = unbox >> canExec
      AutoRequery = autoRequery
      WrapDispatch = id
    })


  let internal subModel
      name
      (getModel: 'model -> 'subModel voption)
      (toMsg: 'subMsg -> 'msg)
      (bindings: Binding<'subModel, 'subMsg> list)
      (sticky: bool) =
    name |> createBinding (SubModelData {
      GetModel = getModel >> ValueOption.map box
      GetBindings = fun () -> bindings |> List.map boxBinding
      ToMsg = unbox<'subMsg> >> toMsg
      Sticky = sticky
    })


  let internal subModelSeq
      name
      (getModels: 'model -> 'subModel list)
      (getId: 'subModel -> 'id)
      (toMsg: 'id * 'subMsg -> 'msg)
      (bindings: Binding<'subModel, 'subMsg> list) =
    name |> createBinding (SubModelSeqData {
      GetModels = getModels >> Seq.map box
      GetId = unbox<'subModel> >> getId >> box
      GetBindings = fun () -> bindings |> List.map boxBinding
      ToMsg = fun (id, msg) -> toMsg (unbox<'id> id, unbox<'subMsg> msg)
    })


  let internal subModelSelectedItem
      name
      subModelSeqBindingName
      (get: 'model -> 'id voption)
      (set: 'id voption -> 'model -> 'msg) =
    name |> createBinding (SubModelSelectedItemData {
      Get = get >> ValueOption.map box
      Set = ValueOption.map unbox<'id> >> set
      SubModelSeqBindingName = subModelSeqBindingName
      WrapDispatch = id
    })



module General =


  [<Fact>]
  let ``throws during instantiation if two bindings have the same name`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m = GenX.auto<int>
      raises<exn> <@ TestVm(m, [oneWay name id; oneWay name id]) @>
  }



module OneWay =


  [<Fact>]
  let ``when retrieved, should always return the value returned by get`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int>
      let! m2 = GenX.auto<int>

      let get = string

      let binding = oneWay name get
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

      let get = string

      let binding = oneWay name get
      let vm = TestVm(m1, binding)

      vm.UpdateModel m2
      test <@ vm.NumPcTriggersFor name = if get m1 = get m2 then 0 else 1 @>
  }



module OneWayLazy =


  [<Fact>]
  let ``when retrieved initially, should return the value returned by map`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m = GenX.auto<int>

      let get = string
      let equals = (=)
      let map = String.length

      let binding = oneWayLazy name get equals map
      let vm = TestVm(m, binding)

      test <@ vm.Get name = (m |> get |> map) @>
  }


  [<Fact>]
  let ``when retrieved after update and equals returns false, should return the value returned by map`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int>
      let! m2 = GenX.auto<int>

      let get = string
      let equals _ _ = false
      let map = String.length

      let binding = oneWayLazy name get equals map
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

      let get = string
      let equals _ _ = true
      let map = String.length

      let binding = oneWayLazy name get equals map
      let vm = TestVm(m1, binding)
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

      let binding = oneWayLazy name get equals map.Fn
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

      let binding = oneWayLazy name get equals map.Fn
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

      let binding = oneWayLazy name get equals map.Fn
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

      let binding = oneWayLazy name get equals map
      let vm = TestVm(m1, binding)
      vm.UpdateModel m2

      test <@ vm.NumPcTriggersFor name = if not eq then 1 else 0 @>
  }


module OneWaySeqLazy =


  [<Fact>]
  let ``when retrieved initially, should return an ObservableCollection with the values returned by map`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m = GenX.auto<int * Guid array>

      let get : int * Guid array -> Guid array = snd
      let equals = (=)
      let map : Guid array -> Guid list = Array.toList
      let itemEquals = (=)
      let getId = id

      let binding = oneWaySeqLazy name get equals map itemEquals getId
      let vm = TestVm(m, binding)

      test <@ (vm.Get name : ObservableCollection<obj>) |> Seq.toList |> List.map unbox = (m |> get |> map) @>
    }


  [<Fact>]
  let ``when retrieved after update and equals returns false, should return an ObservableCollection with the new values returned by map`` () =
      Property.check <| property {
        let! name = GenX.auto<string>
        let! m1 = GenX.auto<int * Guid array>
        let! m2 = GenX.auto<int * Guid array>

        let get : int * Guid array -> Guid array = snd
        let equals _ _ = false
        let map : Guid array -> Guid list = Array.toList
        let itemEquals = (=)
        let getId = id

        let binding = oneWaySeqLazy name get equals map itemEquals getId
        let vm = TestVm(m1, binding)

        vm.UpdateModel m2

        test <@ (vm.Get name : ObservableCollection<obj>) |> Seq.toList |> List.map unbox = (m2 |> get |> map) @>
    }


  [<Fact>]
  let ``when retrieved after update and equals returns true, should return an ObservableCollection with the previous values returned by map`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int * Guid array>
      let! m2 = GenX.auto<int * Guid array>

      let get : int * Guid array -> Guid array = snd
      let equals _ _ = true
      let map : Guid array -> Guid list = Array.toList
      let itemEquals = (=)
      let getId = id

      let binding = oneWaySeqLazy name get equals map itemEquals getId
      let vm = TestVm(m1, binding)

      vm.UpdateModel m2

      test <@ (vm.Get name : ObservableCollection<obj>) |> Seq.toList |> List.map unbox = (m1 |> get |> map) @>
    }


  [<Fact>]
  let ``map should be called at most once during VM instantiation`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int * Guid array>
      let! eq = Gen.bool

      let get = snd
      let equals _ _ = eq
      let map = InvokeTester Array.toList
      let itemEquals = (=)
      let getId = id

      let binding = oneWaySeqLazy name get equals map.Fn itemEquals getId
      TestVm(m1, binding) |> ignore

      test <@ map.Count <= 1 @>
    }


  [<Fact>]
  let ``map should be called at most once during model update iff equals returns false`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int * Guid array>
      let! m2 = GenX.auto<int * Guid array>
      let! eq = Gen.bool

      let get = snd
      let equals _ _ = eq
      let map = InvokeTester Array.toList
      let itemEquals = (=)
      let getId = id

      let binding = oneWaySeqLazy name get equals map.Fn itemEquals getId
      let vm = TestVm(m1, binding)

      map.Reset ()
      vm.UpdateModel m2

      test <@ if eq then map.Count = 0 else map.Count <= 1 @>
    }


  [<Fact>]
  let ``when retrieved several times between updates, map is called at most once`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int * Guid array>
      let! m2 = GenX.auto<int * Guid array>

      let get = snd
      let equals = (=)
      let map = InvokeTester Array.toList
      let itemEquals = (=)
      let getId = id

      let binding = oneWaySeqLazy name get equals map.Fn itemEquals getId
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
  let ``when model is updated, should never trigger PC regardless of equals or itemEquals`` () =  // because this binding should only trigger CC
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int * Guid array>
      let! m2 = GenX.auto<int * Guid array>
      let! eq = Gen.bool
      let! itemEq = Gen.bool

      let get = snd
      let equals _ _ = eq
      let map = InvokeTester Array.toList
      let itemEquals _ _ = itemEq
      let getId = id

      let binding = oneWaySeqLazy name get equals map.Fn itemEquals getId
      let vm = TestVm(m1, binding)

      vm.UpdateModel m2

      test <@ vm.NumPcTriggersFor name = 0 @>
    }


  [<Fact>]
  let ``when model is updated and equals returns true, should never trigger CC`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int * Guid array>
      let! m2 = GenX.auto<int * Guid array>

      let get = snd
      let equals _ _ = true
      let map = Array.toList
      let itemEquals = (=)
      let getId = id

      let binding = oneWaySeqLazy name get equals map itemEquals getId
      let vm = TestVm(m1, binding)

      vm.TrackCcTriggersFor name
      vm.UpdateModel m2

      test <@ vm.NumCcTriggersFor name = 0 @>
    }


  [<Fact>]
  let ``when model is updated and equals returns false, should not trigger CC if elements are identical`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int * Guid array>
      let! i2 = GenX.auto<int>

      let m2 = (i2, snd m1)

      let get = snd
      let equals _ _ = false
      let map = Array.toList
      let itemEquals = (=)
      let getId = id

      let binding = oneWaySeqLazy name get equals map itemEquals getId
      let vm = TestVm(m1, binding)

      vm.TrackCcTriggersFor name
      vm.UpdateModel m2

      test <@ vm.NumCcTriggersFor name = 0 @>
    }


  [<Fact>]
  let ``when model is updated and equals returns false, should trigger CC if elements are added`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int * Guid array>
      let! newItem = Gen.guid
      let! i2 = GenX.auto<int>

      let m2 = (i2, Array.concat [snd m1; [|newItem|]])

      let get = snd
      let equals _ _ = false
      let map = Array.toList
      let itemEquals = (=)
      let getId = id

      let binding = oneWaySeqLazy name get equals map itemEquals getId
      let vm = TestVm(m1, binding)

      vm.TrackCcTriggersFor name
      vm.UpdateModel m2

      test <@ vm.NumCcTriggersFor name > 0 @>
    }


  [<Fact>]
  let ``when model is updated and equals returns false, should trigger CC if elements are removed`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! arr1 = Gen.guid |> Gen.array (Range.exponential 1 50)
      let! i1 = GenX.auto<int>
      let! i2 = GenX.auto<int>

      let m1 = (i1, arr1)
      let m2 = (i2, Array.tail arr1)

      let get = snd
      let equals _ _ = false
      let map = Array.toList
      let itemEquals = (=)
      let getId = id

      let binding = oneWaySeqLazy name get equals map itemEquals getId
      let vm = TestVm(m1, binding)

      vm.TrackCcTriggersFor name
      vm.UpdateModel m2

      test <@ vm.NumCcTriggersFor name > 0 @>
    }


  [<Fact>]
  let ``when model is updated and equals returns false, should trigger CC if elements are re-ordered`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! arr1 = Gen.guid |> Gen.array (Range.exponential 2 50)
      let! i1 = GenX.auto<int>
      let! i2 = GenX.auto<int>

      let m1 = (i1, arr1)
      let m2 = (i2, Array.rev arr1)

      let get = snd
      let equals _ _ = false
      let map = Array.toList
      let itemEquals = (=)
      let getId = id

      let binding = oneWaySeqLazy name get equals map itemEquals getId
      let vm = TestVm(m1, binding)

      vm.TrackCcTriggersFor name
      vm.UpdateModel m2

      test <@ vm.NumCcTriggersFor name > 0 @>
    }


  [<Fact>]
  let ``when model is updated and equals returns false, should trigger CC if itemEquals returns false`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! arr1 = Gen.guid |> Gen.array (Range.exponential 1 50)
      let! arr2 = Gen.guid |> Gen.array (Range.exponential 1 50)
      let! i1 = GenX.auto<int>
      let! i2 = GenX.auto<int>

      let m1 = (i1, arr1)
      let m2 = (i2, arr2)

      let get = snd
      let equals _ _ = false
      let map = Array.toList
      let itemEquals _ _ = false
      let getId = id

      let binding = oneWaySeqLazy name get equals map itemEquals getId
      let vm = TestVm(m1, binding)

      vm.TrackCcTriggersFor name
      vm.UpdateModel m2

      test <@ vm.NumCcTriggersFor name > 0 @>
    }


  type TestClass (id: int, data: string) =
    member _.Id = id
    member _.Data = data
    override _.GetHashCode() = 0
    override _.Equals that =
      // All instances of TestClass are considered equal.
      // Not very helpful, but a valid implementation.
      that :? TestClass

  [<Fact>]
  let ``when equals returns false and element removed from model, should trigger CC.Remove for removed element`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! id1 = GenX.auto<int>
      let! id2 = GenX.auto<int> |> GenX.notEqualTo id1
      let! data1 = GenX.auto<string>
      let! data2 = GenX.auto<string>

      let tc1 = TestClass(id1, data1)
      let tc2 = TestClass(id2, data2)

      let m1 = [tc1; tc2]
      let m2 = [tc1]

      let get = id
      let equals _ _ = false
      let map = id
      let itemEquals _ _ = true
      let getId (tc: TestClass) = tc.Id

      let binding = oneWaySeqLazy name get equals map itemEquals getId
      let vm = TestVm(m1, binding)

      vm.TrackCcTriggersFor name
      vm.UpdateModel m2

      test <@ ((name
        |> vm.CcTriggersFor
        |> List.filter (fun e -> e.Action = NotifyCollectionChangedAction.Remove)
        |> List.head).OldItems.[0] :?> TestClass).Id = tc2.Id @>
    }

  [<Fact>]
  let ``when equals returns false and element updated in model, should trigger CC.Remove or CC.Replace for udpated element`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! id1 = GenX.auto<int>
      let! id2 = GenX.auto<int> |> GenX.notEqualTo id1
      let! data1 = GenX.auto<string>
      let! data2 = GenX.auto<string>
      let! data3 = GenX.auto<string> |> GenX.notEqualTo data2

      let tc1 = TestClass(id1, data1)
      let tc2 = TestClass(id2, data2)
      let tc3 = TestClass(id2, data3)

      let m1 = [tc1; tc2]
      let m2 = [tc1; tc3]

      let get = id
      let equals _ _ = false
      let map = id
      let itemEquals (a: TestClass) (b: TestClass) = a.Data = b.Data
      let getId (tc: TestClass) = tc.Id

      let binding = oneWaySeqLazy name get equals map itemEquals getId
      let vm = TestVm(m1, binding)

      vm.TrackCcTriggersFor name
      vm.UpdateModel m2

      test <@ ((name
        |> vm.CcTriggersFor
        |> List.filter (fun e -> e.Action = NotifyCollectionChangedAction.Remove || e.Action = NotifyCollectionChangedAction.Replace)
        |> List.head).OldItems.[0] :?> TestClass).Id = tc2.Id @>
    }


module TwoWay =


  [<Fact>]
  let ``when retrieved, should always return the value returned by get`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int>
      let! m2 = GenX.auto<int>

      let get = string
      let set _ _ = ()

      let binding = twoWay name get set
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

      let get = string
      let set _ _ = ()

      let binding = twoWay name get set
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

      let binding = twoWay name get set
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

      let get = string
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

      let get = string
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
      let validate m = ValueSome (string m)

      let binding = twoWayValidate name get set validate
      let vm = TestVm(m1, binding)
      let vm' = vm :> INotifyDataErrorInfo

      test <@ vm'.HasErrors = true @>
      test <@ vm'.GetErrors name |> Seq.cast |> Seq.toList = [(validate m1).Value] @>

      vm.UpdateModel m2

      test <@ vm'.HasErrors = true @>
      test <@ vm'.GetErrors name |> Seq.cast |> Seq.toList = [(validate m2).Value] @>
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

      let binding = cmd name exec canExec
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

      let binding = cmd name exec canExec
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

      let binding = cmd name exec canExec
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

      let binding = cmd name exec canExec
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

      test <@ (vm.Get name : ViewModel<obj, obj>).CurrentModel |> unbox = (getModel m1).Value @>

      vm.UpdateModel m2

      test <@ (vm.Get name : ViewModel<obj, obj>).CurrentModel |> unbox = (getModel m2).Value @>
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

      test <@ (vm.Get name : ViewModel<obj, obj>).CurrentModel |> unbox = (getModel m1).Value @>

      vm.UpdateModel m2

      if sticky then
        test <@ (vm.Get name : ViewModel<obj, obj>).CurrentModel |> unbox = (getModel m1).Value @>
      else
        test <@ vm.Get name |> isNull @>

      vm.UpdateModel m3

      test <@ (vm.Get name : ViewModel<obj, obj>).CurrentModel |> unbox = (getModel m3).Value @>
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
      let subGet = string

      let subBinding = oneWay subName subGet
      let binding = subModel name getModel toMsg [subBinding] sticky
      let vm = TestVm(m, binding)

      test <@ (vm.Get name : ViewModel<obj,obj>).Get subName |> unbox = ((getModel m).Value |> subGet) @>
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

      let subBinding = twoWay subName subGet subSet
      let binding = subModel name getModel toMsg [subBinding] sticky
      let vm = TestVm(m, binding)

      (vm.Get name : ViewModel<obj,obj>).Set subName p

      test <@ vm.Dispatches = [subSet p (getModel m).Value |> toMsg] @>
    }



module SubModelSeq =


  [<Fact>]
  let ``when retrieved, should return an ObservableCollection with ViewModels whose CurrentModel is the corresponding value returned by getModels`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m = GenX.auto<int * Guid list>

      let getModels : int * Guid list -> Guid list = snd
      let getId = id
      let toMsg = id

      let binding = subModelSeq name getModels getId toMsg []
      let vm = TestVm(m, binding)

      test <@ vm.Get name
              |> unbox<ObservableCollection<ViewModel<obj,obj>>>
              |> Seq.map (fun vm -> vm.CurrentModel |> unbox)
              |> Seq.toList
                = getModels m
           @>
    }


  [<Fact>]
  let ``when model is updated, should never trigger PC`` () =  // because this binding should only trigger CC
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int * Guid list>
      let! m2 = GenX.auto<int * Guid list>

      let getModels = snd
      let getId = id
      let toMsg = id

      let binding = subModelSeq name getModels getId toMsg []
      let vm = TestVm(m1, binding)

      vm.UpdateModel m2

      test <@ vm.NumPcTriggersFor name = 0 @>
    }


  [<Fact>]
  let ``when model is updated, should not trigger CC if elements are the same`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int * Guid list>
      let! i2 = GenX.auto<int>
      let m2 = (i2, snd m1)

      let getModels = snd
      let getId = id
      let toMsg = id

      let binding = subModelSeq name getModels getId toMsg []
      let vm = TestVm(m1, binding)

      vm.TrackCcTriggersFor name

      vm.UpdateModel m2

      test <@ vm.NumCcTriggersFor name = 0 @>
    }


  [<Fact>]
  let ``when model is updated, should trigger CC if elements are added`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! m1 = GenX.auto<int * Guid list>
      let! newItem = Gen.guid
      let! i2 = GenX.auto<int>

      let m2 = (i2, (snd m1) @ [newItem])

      let getModels = snd
      let getId = id
      let toMsg = id

      let binding = subModelSeq name getModels getId toMsg []
      let vm = TestVm(m1, binding)

      vm.TrackCcTriggersFor name

      vm.UpdateModel m2

      test <@ vm.NumCcTriggersFor name > 0 @>
    }


  [<Fact>]
  let ``when model is updated, should trigger CC if elements are removed`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! list1 = Gen.guid |> Gen.list (Range.exponential 1 50)
      let! i1 = GenX.auto<int>
      let! i2 = GenX.auto<int>

      let m1 = (i1, list1)
      let m2 = (i2, List.tail list1)

      let getModels = snd
      let getId = id
      let toMsg = id

      let binding = subModelSeq name getModels getId toMsg []
      let vm = TestVm(m1, binding)

      vm.TrackCcTriggersFor name

      vm.UpdateModel m2

      test <@ vm.NumCcTriggersFor name > 0 @>
    }


  [<Fact>]
  let ``when model is updated, should trigger CC if elements are re-ordered`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! list = Gen.guid |> Gen.list (Range.exponential 2 50)
      let! i1 = GenX.auto<int>
      let! i2 = GenX.auto<int>

      let m1 = (i1, list)
      let m2 = (i2, List.rev list)

      let getModels = snd
      let getId = id
      let toMsg = id

      let binding = subModelSeq name getModels getId toMsg []
      let vm = TestVm(m1, binding)

      vm.TrackCcTriggersFor name

      vm.UpdateModel m2

      test <@ vm.NumCcTriggersFor name > 0 @>
    }


  [<Fact>]
  let ``smoke test: when a sub-model OneWay binding is retrieved, returns the value returned by get`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! subName = GenX.auto<string>
      let! m = GenX.auto<int * Guid list>

      let getModels : int * Guid list -> Guid list = snd
      let getId = id
      let toMsg = id
      let subGet = string

      let subBinding = oneWay subName subGet
      let binding = subModelSeq name getModels getId toMsg [subBinding]
      let vm = TestVm(m, binding)

      test <@ vm.Get name
              |> unbox<ObservableCollection<ViewModel<obj,obj>>>
              |> Seq.map (fun vm -> vm.Get subName |> unbox<string>)
              |> Seq.toList
                = (getModels m |> Seq.map subGet |> Seq.toList)
              @>
    }


  [<Fact>]
  let ``smoke test: when a sub-model TwoWay binding is set, dispatches the value returned by set transformed by toMsg`` () =
    Property.check <| property {
      let! name = GenX.auto<string>
      let! subName = GenX.auto<string>
      let! m = GenX.auto<int * Guid list>
      let! p = GenX.auto<string>

      let getModels : int * Guid list -> Guid list = snd
      let getId = string
      let toMsg (id: string, subMsg: string) = (id + subMsg).Length
      let subGet = string
      let subSet (p: string) (m: Guid) = p + string m

      let subBinding = twoWay subName subGet subSet
      let binding = subModelSeq name getModels getId toMsg [subBinding]
      let vm = TestVm(m, binding)

      vm.Get name
      |> unbox<ObservableCollection<ViewModel<obj,obj>>>
      |> Seq.iter (fun vm -> vm.Set subName p)

      test <@ vm.Dispatches = (m |> getModels |> List.map (fun m -> (getId m, subSet p m) |> toMsg)) @>
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
          test <@ (vm.Get selectedItemName |> unbox<ViewModel<obj,obj>>) |> Option.ofObj |> Option.map (fun vm -> unbox vm.CurrentModel)
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
          |> unbox<ObservableCollection<ViewModel<obj,obj>>>
          |> Seq.tryFind (fun vm -> vm.CurrentModel |> unbox |> getId = getId sm)
          |> ValueOption.ofOption
        )
        |> ValueOption.toObj

      vm.Set selectedItemName selectedVm

      test <@ vm.Dispatches = [ set (selectedSubModel |> ValueOption.map getId) m ] @>
    }
