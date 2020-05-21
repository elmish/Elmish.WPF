module Elmish.WPF.Tests.MergeTests

open System
open System.Collections.ObjectModel
open System.Collections.Specialized
open Xunit
open Hedgehog
open Swensen.Unquote
open Elmish.WPF


let logNoOp _ _ _ = ()
let getIdAsId = id
let createAsId a _ = a
let updateNoOp _ _ _ = ()
let merge x = x |> historicalMerge logNoOp logNoOp
let simpleMerge x = x |> merge getIdAsId getIdAsId createAsId updateNoOp


let private trackCC (observableCollection: ObservableCollection<_>) =
  let collection = Collection<_> ()
  observableCollection.CollectionChanged.Add collection.Add
  collection

let private testObservableCollectionContainsDataInArray observableCollection array =
  let actual = observableCollection |> Seq.toList
  let expected = array |> Array.toList
  test <@ expected = actual @>

  
module private List =
  let swap i j =
    List.permute
      (function
        | a when a = i -> j
        | a when a = j -> i
        | a -> a)


[<Fact>]
let ``starting from empty, when items merged, should contain those items`` () =
  Property.check <| property {
    let! array = GenX.auto<Guid array>

    let observableCollection = ObservableCollection<_> ()

    simpleMerge observableCollection array

    testObservableCollectionContainsDataInArray observableCollection array
  }

[<Fact>]
let ``starting with random items, when merging the same items, should still contain those items and trigger no CC event`` () =
  Property.check <| property {
    let! array = GenX.auto<Guid array>
    
    let observableCollection = ObservableCollection<_> array
    let ccEvents = trackCC observableCollection
    
    simpleMerge observableCollection array

    testObservableCollectionContainsDataInArray observableCollection array
    test <@ ccEvents.Count = 0 @>
  }

  
[<Fact>]
let ``starting with random items, when merging random items, should contain the random items`` () =
  Property.check <| property {
    let! array1 = GenX.auto<Guid array>
    let! array2 = GenX.auto<Guid array>

    let observableCollection = ObservableCollection<_> array1

    simpleMerge observableCollection array2

    testObservableCollectionContainsDataInArray observableCollection array2
  }

[<Fact>]
let ``starting with random items, when merging after an addition, should contain the merged items`` () =
  Property.check <| property {
    let! list1 = GenX.auto<Guid list>
    let! addedItem = Gen.guid
    let! list2 = list1 |> Gen.constant |> GenX.addElement addedItem
    
    let observableCollection = ObservableCollection<_> list1
    let array2 = list2 |> List.toArray
    
    simpleMerge observableCollection array2

    testObservableCollectionContainsDataInArray observableCollection array2
  }
  
[<Fact>]
let ``starting with random items, when merging after a removal, should contain the merged items`` () =
  Property.check <| property {
    let! list2 = GenX.auto<Guid list>
    let! removedItem = Gen.guid
    let! list1 = list2 |> Gen.constant |> GenX.addElement removedItem
    
    let observableCollection = ObservableCollection<_> list1
    let array2 = list2 |> List.toArray
    
    simpleMerge observableCollection array2

    testObservableCollectionContainsDataInArray observableCollection array2
  }
  
[<Fact>]
let ``starting with random items, when merging after a replacement, should contain the merged items`` () =
  Property.check <| property {
    let! list1Head = Gen.guid
    let! list1Tail = GenX.auto<Guid list>
    let! list2Replacement = Gen.guid
    let! replcementIndex = (0, list1Tail.Length) ||> Range.constant |> Gen.int

    let list1 = list1Head :: list1Tail
    let observableCollection = ObservableCollection<_> list1
    let array2 =
      (list1 |> List.take replcementIndex)
      @ [list2Replacement]
      @ (list1 |> List.skip (replcementIndex + 1))
      |> List.toArray
    
    simpleMerge observableCollection array2

    testObservableCollectionContainsDataInArray observableCollection array2
  }
  
[<Fact>]
let ``starting with random items, when merging after swapping two adjacent items, should contain the merged items`` () =
  Property.check <| property {
    let! list1 = Gen.guid |> Gen.list (Range.exponential 2 50)
    let! firstSwapIndex = (0, list1.Length - 2) ||> Range.constant |> Gen.int

    let observableCollection = ObservableCollection<_> list1
    let array2 =
      list1
      |> List.swap firstSwapIndex (firstSwapIndex + 1)
      |> List.toArray
    
    simpleMerge observableCollection array2

    testObservableCollectionContainsDataInArray observableCollection array2
  }
  
[<Fact>]
let ``starting with random items, when merging after shuffling, should contain the merged items`` () =
  Property.check <| property {
    let! list1 = Gen.guid |> Gen.list (Range.exponential 2 50)
    let! list2 = list1 |> GenX.shuffle |> GenX.notEqualTo list1
    
    let observableCollection = ObservableCollection<_> list1
    let array2 = list2 |> List.toArray
    
    simpleMerge observableCollection array2

    testObservableCollectionContainsDataInArray observableCollection array2
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
let ``starting with two TestClass instances, when merging after updating the last one, should update on updated item`` () =
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
