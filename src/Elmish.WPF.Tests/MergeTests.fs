module Elmish.WPF.Tests.MergeTests

open System
open System.Collections.ObjectModel
open System.Collections.Specialized
open Xunit
open Hedgehog
open Swensen.Unquote
open Elmish.WPF


let getIdAsId = id
let createAsId a _ = a
let updateNoOp _ _ _ = ()
let merge x = x |> elmStyleMerge


let private trackCC (observableCollection: ObservableCollection<_>) =
  let collection = Collection<_> ()
  observableCollection.CollectionChanged.Add collection.Add
  collection

let private testObservableCollectionContainsDataInArray observableCollection array =
  let actual = observableCollection |> Seq.toList
  let expected = array |> Array.toList
  test <@ expected = actual @>


[<Fact>]
let ``starting from empty, when items merged, should contain those items and call create exactly once for each item and never call update`` () =
  Property.check <| property {
    let! array = GenX.auto<Guid array>

    let observableCollection = ObservableCollection<_> ()
    let createTracker = InvokeTester2 createAsId
    let updateTracker = InvokeTester3 updateNoOp

    merge getIdAsId getIdAsId createTracker.Fn updateTracker.Fn observableCollection array

    testObservableCollectionContainsDataInArray observableCollection array
    test <@ createTracker.Count = array.Length @>
    test <@ updateTracker.Count = 0 @>
  }

[<Fact>]
let ``starting with random items, when merging the same items, should still contain those items and never call create and call update exactly once for each item and trigger no CC event`` () =
  Property.check <| property {
    let! array = GenX.auto<Guid array>
    
    let observableCollection = ObservableCollection<_> array
    let createTracker = InvokeTester2 createAsId
    let updateTracker = InvokeTester3 updateNoOp
    let ccEvents = trackCC observableCollection
    
    merge getIdAsId getIdAsId createTracker.Fn updateTracker.Fn observableCollection array

    testObservableCollectionContainsDataInArray observableCollection array
    test <@ createTracker.Count = 0 @>
    test <@ updateTracker.Count = array.Length @>
    test <@ ccEvents.Count = 0 @>
  }

  
[<Fact>]
let ``starting with random items, when merging random items, should contain the random items`` () =
  Property.check <| property {
    let! array1 = GenX.auto<Guid array>
    let! array2 = GenX.auto<Guid array>

    let observableCollection = ObservableCollection<_> array1

    merge getIdAsId getIdAsId createAsId updateNoOp observableCollection array2

    testObservableCollectionContainsDataInArray observableCollection array2
  }

[<Fact>]
let ``starting with random items, when merging after an addition, should contain the merged items and call create exactly once and call update exactly once for each original item`` () =
  Property.check <| property {
    let! list1 = GenX.auto<Guid list>
    let! addedItem = Gen.guid
    let! list2 = list1 |> Gen.constant |> GenX.addElement addedItem
    
    let observableCollection = ObservableCollection<_> list1
    let array2 = list2 |> List.toArray
    let createTracker = InvokeTester2 createAsId
    let updateTracker = InvokeTester3 updateNoOp
    
    merge getIdAsId getIdAsId createTracker.Fn updateTracker.Fn observableCollection array2

    testObservableCollectionContainsDataInArray observableCollection array2
    test <@ createTracker.Count = 1 @>
    test <@ updateTracker.Count = array2.Length - 1 @>
  }
  
[<Fact>]
let ``starting with random items, when merging after a removal, should contain the merged items and never call create and call update exactly once for each remaining item`` () =
  Property.check <| property {
    let! list2 = GenX.auto<Guid list>
    let! removedItem = Gen.guid
    let! list1 = list2 |> Gen.constant |> GenX.addElement removedItem
    
    let observableCollection = ObservableCollection<_> list1
    let array2 = list2 |> List.toArray
    let createTracker = InvokeTester2 createAsId
    let updateTracker = InvokeTester3 updateNoOp
    
    merge getIdAsId getIdAsId createTracker.Fn updateTracker.Fn observableCollection array2

    testObservableCollectionContainsDataInArray observableCollection array2
    test <@ createTracker.Count = 0 @>
    test <@ updateTracker.Count = array2.Length @>
  }
  
[<Fact>]
let ``starting with random items, when merging after a move, should contain the merged items and never call create and call update exactly once for each item`` () =
  Property.check <| property {
    let! list = GenX.auto<Guid list>
    let! movedItem = Gen.guid
    let! additionalItem = Gen.guid
    let! i1 = (0, list.Length + 1) ||> Range.constant |> Gen.int
    let! i2 = (0, list.Length + 1) ||> Range.constant |> Gen.int |> GenX.notEqualTo i1

    let list = additionalItem :: list
    let list1 = list |> List.insert i1 movedItem
    let array2 = list |> List.insert i2 movedItem |> List.toArray
    let observableCollection = ObservableCollection<_> list1
    let createTracker = InvokeTester2 createAsId
    let updateTracker = InvokeTester3 updateNoOp
    
    merge getIdAsId getIdAsId createTracker.Fn updateTracker.Fn observableCollection array2

    testObservableCollectionContainsDataInArray observableCollection array2
    test <@ createTracker.Count = 0 @>
    test <@ updateTracker.Count = array2.Length @>
  }
  
[<Fact>]
let ``starting with random items, when merging after a replacement, should contain the merged items and call create exactly once and call update exactly once for each original item that remains`` () =
  Property.check <| property {
    let! list1Head = Gen.guid
    let! list1Tail = GenX.auto<Guid list>
    let! list2Replacement = Gen.guid
    let! replcementIndex = (0, list1Tail.Length) ||> Range.constant |> Gen.int

    let list1 = list1Head :: list1Tail
    let observableCollection = ObservableCollection<_> list1
    let array2 =
      list1
      |> List.replace replcementIndex list2Replacement
      |> List.toArray
    let createTracker = InvokeTester2 createAsId
    let updateTracker = InvokeTester3 updateNoOp
    
    merge getIdAsId getIdAsId createTracker.Fn updateTracker.Fn observableCollection array2

    testObservableCollectionContainsDataInArray observableCollection array2
    test <@ createTracker.Count = 1 @>
    test <@ updateTracker.Count = array2.Length - 1 @>
  }
  
[<Fact>]
let ``starting with random items, when merging after swapping two adjacent items, should contain the merged items and never call create and call update exactly once for each item`` () =
  Property.check <| property {
    let! list1 = Gen.guid |> Gen.list (Range.exponential 2 50)
    let! firstSwapIndex = (0, list1.Length - 2) ||> Range.constant |> Gen.int

    let observableCollection = ObservableCollection<_> list1
    let array2 =
      list1
      |> List.swap firstSwapIndex (firstSwapIndex + 1)
      |> List.toArray
    let createTracker = InvokeTester2 createAsId
    let updateTracker = InvokeTester3 updateNoOp
    
    merge getIdAsId getIdAsId createTracker.Fn updateTracker.Fn observableCollection array2

    testObservableCollectionContainsDataInArray observableCollection array2
    test <@ createTracker.Count = 0 @>
    test <@ updateTracker.Count = array2.Length @>
  }
  
[<Fact>]
let ``starting with random items, when merging after swapping two items, should contain the merged items and never call create and call update exactly once for each item`` () =
  Property.check <| property {
    let! list1 = Gen.guid |> Gen.list (Range.exponential 2 50)
    let! i = (0, list1.Length - 1) ||> Range.constant |> Gen.int
    let! j = (0, list1.Length - 1) ||> Range.constant |> Gen.int |> GenX.notEqualTo i

    let observableCollection = ObservableCollection<_> list1
    let array2 =
      list1
      |> List.swap i j
      |> List.toArray
    let createTracker = InvokeTester2 createAsId
    let updateTracker = InvokeTester3 updateNoOp
    
    merge getIdAsId getIdAsId createTracker.Fn updateTracker.Fn observableCollection array2

    testObservableCollectionContainsDataInArray observableCollection array2
    test <@ createTracker.Count = 0 @>
    test <@ updateTracker.Count = array2.Length @>
  }
  
[<Fact>]
let ``starting with random items, when merging after shuffling, should contain the merged items and never call create and call update eactly once for each item`` () =
  Property.check <| property {
    let! list1 = Gen.guid |> Gen.list (Range.exponential 2 50)
    let! list2 = list1 |> GenX.shuffle |> GenX.notEqualTo list1
    
    let observableCollection = ObservableCollection<_> list1
    let array2 = list2 |> List.toArray
    let createTracker = InvokeTester2 createAsId
    let updateTracker = InvokeTester3 updateNoOp
    
    merge getIdAsId getIdAsId createTracker.Fn updateTracker.Fn observableCollection array2

    testObservableCollectionContainsDataInArray observableCollection array2
    test <@ createTracker.Count = 0 @>
    test <@ updateTracker.Count = array2.Length @>
  }
  
type TestClass (id: int, data: string) =
  member _.Id = id
  member _.Data = data
  override __.GetHashCode() = 0
  override __.Equals that =
    // All instances of TestClass are considered equal.
    // Not very helpful, but a valid implementation.
    that :? TestClass

[<Fact>]
let ``starting with two TestClass instances, when merging after removing the last one, should trigger CC.Remove for removed item`` () =
  Property.check <| property {
    let! id1 = GenX.auto<int>
    let! id2 = GenX.auto<int> |> GenX.notEqualTo id1
    let! data1 = GenX.auto<string>
    let! data2 = GenX.auto<string>

    let tc1 = TestClass(id1, data1)
    let tc2 = TestClass(id2, data2)
    let array1 = [| tc1; tc2 |]
    let array2 = [| tc1 |]
    let observableCollection = ObservableCollection<_> array1
    let ccEvents = trackCC observableCollection
    let getId (tc: TestClass) = tc.Id
    
    merge getId getId createAsId updateNoOp observableCollection array2

    test <@ ((ccEvents
      |> Seq.filter (fun e -> e.Action = NotifyCollectionChangedAction.Remove)
      |> Seq.head).OldItems.[0] :?> TestClass).Id = tc2.Id @>
  }

[<Fact>]
let ``starting with two TestClass instances, when merging after updating the last one, should call update on updated item`` () =
  Property.check <| property {
    let! id1 = GenX.auto<int>
    let! id2 = GenX.auto<int> |> GenX.notEqualTo id1
    let! data1 = GenX.auto<string>
    let! data2 = GenX.auto<string>
    let! data3 = GenX.auto<string> |> GenX.notEqualTo data2

    let tc1 = TestClass(id1, data1)
    let tc2 = TestClass(id2, data2)
    let tc3 = TestClass(id2, data3)
    let array1 = [| tc1; tc2 |]
    let array2 = [| tc1; tc3 |]
    let observableCollection = ObservableCollection<_> array1
    let getId (tc: TestClass) = tc.Id

    let mutable mTarget = None
    let update target _ _ =
      mTarget <- Some target

    merge getId getId createAsId update observableCollection array2

    let actual = mTarget
    test <@ actual.Value.Id = tc2.Id @>
  }
