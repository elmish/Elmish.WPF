module Elmish.WPF.Tests.BindingTests

open Xunit
open Hedgehog
open Swensen.Unquote
open Elmish.WPF


[<AutoOpen>]
module internal Helpers =

  let fail _ = failwith "Placeholder function was invoked"
  let fail2 _ _ = failwith "Placeholder function was invoked"

  let rec getBaseBindingData = function
    | BaseBindingData d -> d
    | CachingData d -> getBaseBindingData d
    | ValidationData d -> getBaseBindingData d.BindingData
    | LazyData d -> getBaseBindingData d.BindingData
    | AlterMsgStreamData _ -> raise (System.NotSupportedException()) // hack: reasonable because this is test code and the tests don't currently use this case

  let getOneWayData f =
    match getBaseBindingData (f "").Data with
    | OneWayData d -> d
    | _ -> failwith "Incorrect binding"

  let getOneWaySeqLazyData f =
    match getBaseBindingData (f "").Data with
    | OneWaySeqLazyData d -> d
    | _ -> failwith "Incorrect binding"

  let getTwoWayData f =
    match getBaseBindingData (f "").Data with
    | TwoWayData d -> d
    | _ -> failwith "Incorrect binding"

  let getCmdData f =
    match getBaseBindingData (f "").Data with
    | CmdData d -> d
    | _ -> failwith "Incorrect binding"

  let getSubModelData f =
    match getBaseBindingData (f "").Data with
    | SubModelData d -> d
    | _ -> failwith "Incorrect binding"

  let getSubModelSeqKeyedData f =
    match getBaseBindingData (f "").Data with
    | SubModelSeqKeyedData d -> d
    | _ -> failwith "Incorrect binding"

  let getSubModelSelectedItemData f =
    match getBaseBindingData (f "").Data with
    | SubModelSelectedItemData d -> d
    | _ -> failwith "Incorrect binding"

  let getValidationData f =
    match f "" with
    | { Data = ValidationData d } -> d
    | _ -> failwith "Incorrect binding"



module oneWay =


  [<Fact>]
  let ``sets the correct binding name`` () =
    Property.check <| property {
      let! bindingName = GenX.auto<string>
      let binding = bindingName |> Binding.OneWay.id
      test <@ binding.Name = bindingName @>
    }


  [<Fact>]
  let ``final get returns value from original get`` () =
    Property.check <| property {
      let! x = GenX.auto<int>

      let get = string<int>
      let d = Binding.oneWay(get) |> getOneWayData

      test <@ d.Get x |> unbox = get x @>
    }


module oneWayOpt =


  module option =


    [<Fact>]
    let ``when original get returns Some, final get returns the inner value`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get = string >> Some
        let d = Binding.oneWayOpt(get) |> getOneWayData

        test <@ d.Get x |> unbox = (get x).Value @>
      }


    [<Fact>]
    let ``when original get returns None, final get returns null`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get _ = None
        let d = Binding.oneWayOpt(get) |> getOneWayData

        test <@ isNull (d.Get x) @>
      }



  module voption =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.oneWayOpt((fail: _ -> _ voption))
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``when original get returns ValueSome, final get returns the inner value`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get = string >> ValueSome
        let d = Binding.oneWayOpt(get) |> getOneWayData

        test <@ d.Get x |> unbox = (get x).Value @>
      }


    [<Fact>]
    let ``when original get returns ValueNone, final get returns null`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get _ = ValueNone
        let d = Binding.oneWayOpt(get) |> getOneWayData

        test <@ isNull (d.Get x) @>
      }



module oneWaySeq =


  [<Fact>]
  let ``sets the correct binding name`` () =
    Property.check <| property {
      let! bindingName = GenX.auto<string>
      let binding = bindingName |> Binding.oneWaySeq(fail, fail2, fail)
      test <@ binding.Name = bindingName @>
    }


  [<Fact>]
  let ``final get returns value from original get`` () =
    Property.check <| property {
      let! x = GenX.auto<int>

      let get = string<int>
      let d = Binding.oneWaySeq(get, fail2, fail) |> getOneWaySeqLazyData

      test <@ d.Get x |> unbox = get x @>
    }


  [<Fact>]
  let ``final map returns the seq its given`` () =
    Property.check <| property {
      let! array = Gen.guid |> Gen.array (Range.constant 1 50)

      let list = array |> Array.toList
      let d = Binding.oneWaySeq(fail, fail2, fail) |> getOneWaySeqLazyData

      test <@ list |> Seq.map box |> box |> d.Map |> Seq.map unbox |> Seq.toList = list @>
    }


  [<Fact>]
  let ``final equals returns true for the same sequence references`` () =
    Property.check <| property {
      let! array = Gen.guid |> Gen.array (Range.constant 1 50)

      let list = array |> Seq.map box |> Seq.toList
      let d = Binding.oneWaySeq(fail, fail2, fail) |> getOneWaySeqLazyData

      test <@ d.Equals (box list) (box list) = true @>
    }


  [<Fact>]
  let ``final equals returns false for different reference sequences`` () =
    Property.check <| property {
      let! array = Gen.guid |> Gen.array (Range.constant 1 50)

      let list1 = array |> Seq.map box |> Seq.toList
      let list2 = list1 |> Seq.map id |> Seq.toList
      let d = Binding.oneWaySeq(fail, fail2, fail) |> getOneWaySeqLazyData

      test <@ refEq list1 list2 = false @> // ensure lists are not reference equal
      test <@ d.Equals (box list1) (box list2) = false @>
    }


  [<Fact>]
  let ``final getId returns value from original getId`` () =
    Property.check <| property {
      let! x = GenX.auto<int>

      let getId = string<int>
      let d = Binding.oneWaySeq(fail, fail2, getId) |> getOneWaySeqLazyData

      test <@ d.GetId (box x) |> unbox = getId x @>
    }


  [<Fact>]
  let ``final itemEquals returns value from original itemEquals`` () =
    Property.check <| property {
      let! x = GenX.auto<int>
      let! y = GenX.auto<int>

      let itemEquals : int -> int -> bool = (=)
      let d = Binding.oneWaySeq(fail, itemEquals, fail) |> getOneWaySeqLazyData

      test <@ d.ItemEquals (box x) (box y) = itemEquals x y @>
    }



module oneWaySeqLazy =


  [<Fact>]
  let ``sets the correct binding name`` () =
    Property.check <| property {
      let! bindingName = GenX.auto<string>
      let binding = bindingName |> Binding.oneWaySeqLazy(fail, fail2, fail, fail2, fail)
      test <@ binding.Name = bindingName @>
    }


  [<Fact>]
  let ``final get returns value from original get`` () =
    Property.check <| property {
      let! x = GenX.auto<int>

      let get = string<int>
      let d = Binding.oneWaySeqLazy(get, fail2, fail, fail2, fail) |> getOneWaySeqLazyData

      test <@ d.Get x |> unbox = get x @>
    }


  [<Fact>]
  let ``final equals returns value from original equals`` () =
    Property.check <| property {
      let! x = GenX.auto<int>
      let! y = GenX.auto<int>

      let equals : int -> int -> bool = (=)
      let d = Binding.oneWaySeqLazy(fail, equals, fail, fail2, fail) |> getOneWaySeqLazyData

      test <@ d.Equals (box x) (box y) = equals x y @>
    }


  [<Fact>]
  let ``final map returns value from original map`` () =
    Property.check <| property {
      let! x = GenX.auto<string>

      let map : string -> char list = Seq.toList
      let d = Binding.oneWaySeqLazy(fail, fail2, map, fail2, fail) |> getOneWaySeqLazyData

      test <@ d.Map (box x) |> Seq.map unbox |> Seq.toList = map x @>
    }


  [<Fact>]
  let ``final getId returns value from original getId`` () =
    Property.check <| property {
      let! x = GenX.auto<int>

      let getId = string<int>
      let d = Binding.oneWaySeqLazy(fail, fail2, fail, fail2, getId) |> getOneWaySeqLazyData

      test <@ d.GetId (box x) |> unbox = getId x @>
    }


  [<Fact>]
  let ``final itemEquals returns value from original itemEquals`` () =
    Property.check <| property {
      let! x = GenX.auto<int>
      let! y = GenX.auto<int>

      let itemEquals : int -> int -> bool = (=)
      let d = Binding.oneWaySeqLazy(fail, fail2, fail, itemEquals, fail) |> getOneWaySeqLazyData

      test <@ d.ItemEquals (box x) (box y) = itemEquals x y @>
    }



module twoWay =


  module setModel =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.TwoWay.id
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final get returns value from original get`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get = string<int>
        let d = Binding.twoWay(get, fail2) |> getTwoWayData

        test <@ d.Get x |> unbox = get x @>
      }


    [<Fact>]
    let ``final set returns value from original set`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let set (p: string) (m: int) = p + string m
        let d = Binding.twoWay((fun _ -> ""), set) |> getTwoWayData

        test <@ d.Set (box p) m |> unbox = set p m @>
      }



  module noSetModel =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.twoWay(fail, (fail: string -> int))
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final get returns value from original get`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get = string<int>
        let d = Binding.twoWay(get, (fail: string -> int)) |> getTwoWayData

        test <@ d.Get x |> unbox = get x @>
      }


    [<Fact>]
    let ``final set returns value from original set`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let set (p: string) = p + p
        let d = Binding.twoWay((fun _ -> ""), set) |> getTwoWayData

        test <@ d.Set (box p) m |> unbox = set p @>
      }



module twoWayOpt =


  module option_setModel =


    [<Fact>]
    let ``when original get returns Some, final get returns the inner value`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get = string >> Some
        let d = Binding.twoWayOpt(get, fail2) |> getTwoWayData

        test <@ d.Get x |> unbox = (get x).Value @>
      }


    [<Fact>]
    let ``when original get returns None, final get returns null`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get _ = None
        let d = Binding.twoWayOpt(get, fail2) |> getTwoWayData

        test <@ isNull (d.Get x) @>
      }


    [<Fact>]
    let ``when final set receives a non-null value, original get receives Some`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let set (p: string option) (m: int) = p |> Option.map ((+) (string m))
        let d = Binding.twoWayOpt((fun _ -> Some ""), set) |> getTwoWayData

        test <@ d.Set (box p) m |> unbox = set (Some p) m @>
      }


    [<Fact>]
    let ``when final set receives null, original get receives None`` () =
      Property.check <| property {
        let! m = GenX.auto<int>

        let set (p: string option) (m: int) = p |> Option.map ((+) (string m))
        let d = Binding.twoWayOpt((fun _ -> Some ""), set) |> getTwoWayData

        test <@ d.Set null m |> unbox = set None m @>
      }



  module voption_setModel =


    [<Fact>]
    let ``when original get returns ValueSome, final get returns the inner value`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get = string >> ValueSome
        let d = Binding.twoWayOpt(get, fail2) |> getTwoWayData

        test <@ d.Get x |> unbox = (get x).Value @>
      }


    [<Fact>]
    let ``when original get returns ValueNone, final get returns null`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get _ = ValueNone
        let d = Binding.twoWayOpt(get, fail2) |> getTwoWayData

        test <@ isNull (d.Get x) @>
      }


    [<Fact>]
    let ``when final set receives a non-null value, original get receives ValueSome`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let set (p: string voption) (m: int) = p |> ValueOption.map ((+) (string m))
        let d = Binding.twoWayOpt((fun _ -> ValueSome ""), set) |> getTwoWayData

        test <@ d.Set (box p) m |> unbox = set (ValueSome p) m @>
      }


    [<Fact>]
    let ``when final set receives null, original get receives ValueNone`` () =
      Property.check <| property {
        let! m = GenX.auto<int>

        let set (p: string voption) (m: int) = p |> ValueOption.map ((+) (string m))
        let d = Binding.twoWayOpt((fun _ -> ValueSome ""), set) |> getTwoWayData

        test <@ d.Set null m |> unbox = set ValueNone m @>
      }



  module option_noSetModel =


    [<Fact>]
    let ``when original get returns Some, final get returns the inner value`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get = string >> Some
        let d = Binding.twoWayOpt(get, (fail: _ option -> int)) |> getTwoWayData

        test <@ d.Get x |> unbox = (get x).Value @>
      }


    [<Fact>]
    let ``when original get returns None, final get returns null`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get _ = None
        let d = Binding.twoWayOpt(get, (fail: _ option -> int)) |> getTwoWayData

        test <@ isNull (d.Get x) @>
      }


    [<Fact>]
    let ``when final set receives a non-null value, original get receives Some`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let set (p: string option) = p |> Option.map ((+) (string m))
        let d = Binding.twoWayOpt((fun _ -> Some ""), set) |> getTwoWayData

        test <@ d.Set (box p) m |> unbox = set (Some p) @>
      }


    [<Fact>]
    let ``when final set receives null, original get receives None`` () =
      Property.check <| property {
        let! m = GenX.auto<int>

        let set (p: string option) = p |> Option.map ((+) (string m))
        let d = Binding.twoWayOpt((fun _ -> Some ""), set) |> getTwoWayData

        test <@ d.Set null m |> unbox = set None @>
      }



  module voption_noSetModel =


    [<Fact>]
    let ``when original get returns ValueSome, final get returns the inner value`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get = string >> ValueSome
        let d = Binding.twoWayOpt(get, (fail: _ voption -> int)) |> getTwoWayData

        test <@ d.Get x |> unbox = (get x).Value @>
      }


    [<Fact>]
    let ``when original get returns ValueNone, final get returns null`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get _ = ValueNone
        let d = Binding.twoWayOpt(get, (fail: _ voption -> int)) |> getTwoWayData

        test <@ isNull (d.Get x) @>
      }


    [<Fact>]
    let ``when final set receives a non-null value, original get receives ValueSome`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let set (p: string voption) = p |> ValueOption.map ((+) (string m))
        let d = Binding.twoWayOpt((fun _ -> ValueSome ""), set) |> getTwoWayData

        test <@ d.Set (box p) m |> unbox = set (ValueSome p) @>
      }


    [<Fact>]
    let ``when final set receives null, original get receives ValueNone`` () =
      Property.check <| property {
        let! m = GenX.auto<int>

        let set (p: string voption) = p |> ValueOption.map ((+) (string m))
        let d = Binding.twoWayOpt((fun _ -> ValueSome ""), set) |> getTwoWayData

        test <@ d.Set null m |> unbox = set ValueNone @>
      }



module twoWayValidate =


  module setModel_validateVoption =


    //[<Fact>]
    //let ``final get returns value from original get`` () =
    //  Property.check <| property {
    //    let! x = GenX.auto<int>

    //    let get = string<int>
    //    let d = Binding.twoWayValidate(get, fail2, (fail: _ -> _ voption)) |> getValidationData

    //    test <@ d.Get x |> unbox = get x @>
    //  }


    //[<Fact>]
    //let ``final set returns value from original set`` () =
    //  Property.check <| property {
    //    let! m = GenX.auto<int>
    //    let! p = GenX.auto<string>

    //    let set (p: string) (m: int) = p + string m
    //    let d = Binding.twoWayValidate(fail, set, (fail: _ -> _ voption)) |> getValidationData

    //    test <@ d.Set (box p) m |> unbox = set p m @>
    //  }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then [ err ] else []
        let d = Binding.twoWayValidate(fail, fail2, validate) |> getValidationData

        test <@ d.Validate x |> unbox = validate x @>
      }



  module setModel_validateOption =


    //[<Fact>]
    //let ``final get returns value from original get`` () =
    //  Property.check <| property {
    //    let! x = GenX.auto<int>

    //    let get = string<int>
    //    let d = Binding.twoWayValidate(get, fail2, (fail: _ -> _ option)) |> getValidationData

    //    test <@ d.Get x |> unbox = get x @>
    //  }


    //[<Fact>]
    //let ``final set returns value from original set`` () =
    //  Property.check <| property {
    //    let! m = GenX.auto<int>
    //    let! p = GenX.auto<string>

    //    let set (p: string) (m: int) = p + string m
    //    let d = Binding.twoWayValidate(fail, set, (fail: _ -> _ option)) |> getValidationData

    //    test <@ d.Set (box p) m |> unbox = set p m @>
    //  }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then [ err ] else []
        let d = Binding.twoWayValidate(fail, fail2, validate) |> getValidationData

        test <@ d.Validate x |> unbox = validate x @>
      }



  module setModel_validateResult =


    //[<Fact>]
    //let ``final get returns value from original get`` () =
    //  Property.check <| property {
    //    let! x = GenX.auto<int>

    //    let get = string<int>
    //    let d = Binding.twoWayValidate(get, fail2, (fail: _ -> Result<_,_>)) |> getValidationData

    //    test <@ d.Get x |> unbox = get x @>
    //  }


    //[<Fact>]
    //let ``final set returns value from original set`` () =
    //  Property.check <| property {
    //    let! m = GenX.auto<int>
    //    let! p = GenX.auto<string>

    //    let set (p: string) (m: int) = p + string m
    //    let d = Binding.twoWayValidate(fail, set, (fail: _ -> Result<_,_>)) |> getValidationData

    //    test <@ d.Set (box p) m |> unbox = set p m @>
    //  }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then [] else [ err ]
        let d = Binding.twoWayValidate(fail, fail2, validate) |> getValidationData

        test <@ d.Validate x |> unbox = validate x @>
      }



  module noSetModel_validateVoption =


    //[<Fact>]
    //let ``final get returns value from original get`` () =
    //  Property.check <| property {
    //    let! x = GenX.auto<int>

    //    let get = string<int>
    //    let d = Binding.twoWayValidate(get, (fail: string -> int), (fail: _ -> _ voption)) |> getValidationData

    //    test <@ d.Get x |> unbox = get x @>
    //  }


    //[<Fact>]
    //let ``final set returns value from original set`` () =
    //  Property.check <| property {
    //    let! m = GenX.auto<int>
    //    let! p = GenX.auto<string>

    //    let set (p: string) = p + p
    //    let d = Binding.twoWayValidate(fail, set, (fail: _ -> _ voption)) |> getValidationData

    //    test <@ d.Set (box p) m |> unbox = set p @>
    //  }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then [ err ] else []
        let d = Binding.twoWayValidate(fail, (fail: string -> int), validate) |> getValidationData

        test <@ d.Validate x |> unbox = validate x @>
      }



  module noSetModel_validateOption =


    //[<Fact>]
    //let ``final get returns value from original get`` () =
    //  Property.check <| property {
    //    let! x = GenX.auto<int>

    //    let get = string<int>
    //    let d = Binding.twoWayValidate(get, (fail: string -> int), (fail: _ -> _ option)) |> getValidationData

    //    test <@ d.Get x |> unbox = get x @>
    //  }


    //[<Fact>]
    //let ``final set returns value from original set`` () =
    //  Property.check <| property {
    //    let! m = GenX.auto<int>
    //    let! p = GenX.auto<string>

    //    let set (p: string) = p + p
    //    let d = Binding.twoWayValidate(fail, set, (fail: _ -> _ option)) |> getValidationData

    //    test <@ d.Set (box p) m |> unbox = set p @>
    //  }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then [ err ] else []
        let d = Binding.twoWayValidate(fail, (fail: string -> int), validate) |> getValidationData

        test <@ d.Validate x |> unbox = validate x @>
      }



  module noSetModel_validateResult =


    //[<Fact>]
    //let ``final get returns value from original get`` () =
    //  Property.check <| property {
    //    let! x = GenX.auto<int>

    //    let get = string<int>
    //    let d = Binding.twoWayValidate(get, (fail: string -> int), (fail: _ -> Result<_,_>)) |> getValidationData

    //    test <@ d.Get x |> unbox = get x @>
    //  }


    //[<Fact>]
    //let ``final set returns value from original set`` () =
    //  Property.check <| property {
    //    let! m = GenX.auto<int>
    //    let! p = GenX.auto<string>

    //    let set (p: string) = p + p
    //    let d = Binding.twoWayValidate(fail, set, (fail: _ -> Result<_,_>)) |> getValidationData

    //    test <@ d.Set (box p) m |> unbox = set p @>
    //  }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then [] else [ err ]
        let d = Binding.twoWayValidate(fail, (fail: string -> int), validate) |> getValidationData

        test <@ d.Validate x |> unbox = validate x @>
      }



module twoWayOptValidate =


  module voption_setModel_validateVoption =


    //[<Fact>]
    //let ``when original get returns ValueSome, final get returns the inner value`` () =
    //  Property.check <| property {
    //    let! x = GenX.auto<int>

    //    let get = string >> ValueSome
    //    let d = Binding.twoWayOptValidate(get, fail2, (fail: _ -> _ voption)) |> getValidationData

    //    test <@ d.Get x |> unbox = (get x).Value @>
    //  }


    //[<Fact>]
    //let ``when original get returns ValueNone, final get returns null`` () =
    //  Property.check <| property {
    //    let! x = GenX.auto<int>

    //    let get _ = ValueNone
    //    let d = Binding.twoWayOptValidate(get, fail2, (fail: _ -> _ voption)) |> getValidationData

    //    test <@ isNull (d.Get x) @>
    //  }


    //[<Fact>]
    //let ``when final set receives a non-null value, original get receives ValueSome`` () =
    //  Property.check <| property {
    //    let! m = GenX.auto<int>
    //    let! p = GenX.auto<string>

    //    let set (p: string voption) (m: int) = p |> ValueOption.map ((+) (string m))
    //    let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> _ voption)) |> getValidationData

    //    test <@ d.Set (box p) m |> unbox = set (ValueSome p) m @>
    //  }


    //[<Fact>]
    //let ``when final set receives null, original get receives ValueNone`` () =
    //  Property.check <| property {
    //    let! m = GenX.auto<int>

    //    let set (p: string voption) (m: int) = p |> ValueOption.map ((+) (string m))
    //    let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> _ voption)) |> getValidationData

    //    test <@ d.Set null m |> unbox = set ValueNone m @>
    //  }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then [ err ] else []
        let d = Binding.twoWayOptValidate((fail: _ -> _ voption), fail2, validate) |> getValidationData

        test <@ d.Validate x |> unbox = validate x @>
      }



  module voption_setModel_validateOption =


    //[<Fact>]
    //let ``when original get returns ValueSome, final get returns the inner value`` () =
    //  Property.check <| property {
    //    let! x = GenX.auto<int>

    //    let get = string >> ValueSome
    //    let d = Binding.twoWayOptValidate(get, fail2, (fail: _ -> _ option)) |> getValidationData

    //    test <@ d.Get x |> unbox = (get x).Value @>
    //  }


    //[<Fact>]
    //let ``when original get returns ValueNone, final get returns null`` () =
    //  Property.check <| property {
    //    let! x = GenX.auto<int>

    //    let get _ = ValueNone
    //    let d = Binding.twoWayOptValidate(get, fail2, (fail: _ -> _ option)) |> getValidationData

    //    test <@ isNull (d.Get x) @>
    //  }


    //[<Fact>]
    //let ``when final set receives a non-null value, original get receives ValueSome`` () =
    //  Property.check <| property {
    //    let! m = GenX.auto<int>
    //    let! p = GenX.auto<string>

    //    let set (p: string voption) (m: int) = p |> ValueOption.map ((+) (string m))
    //    let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> _ option)) |> getValidationData

    //    test <@ d.Set (box p) m |> unbox = set (ValueSome p) m @>
    //  }


    //[<Fact>]
    //let ``when final set receives null, original get receives ValueNone`` () =
    //  Property.check <| property {
    //    let! m = GenX.auto<int>

    //    let set (p: string voption) (m: int) = p |> ValueOption.map ((+) (string m))
    //    let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> _ option)) |> getValidationData

    //    test <@ d.Set null m |> unbox = set ValueNone m @>
    //  }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then [ err ] else []
        let d = Binding.twoWayOptValidate((fail: _ -> _ voption), fail2, validate) |> getValidationData

        test <@ d.Validate x |> unbox = validate x @>
      }



  module voption_setModel_validateResult =


    //[<Fact>]
    //let ``when original get returns ValueSome, final get returns the inner value`` () =
    //  Property.check <| property {
    //    let! x = GenX.auto<int>

    //    let get = string >> ValueSome
    //    let d = Binding.twoWayOptValidate(get, fail2, (fail: _ -> Result<_,_>)) |> getValidationData

    //    test <@ d.Get x |> unbox = (get x).Value @>
    //  }


    //[<Fact>]
    //let ``when original get returns ValueNone, final get returns null`` () =
    //  Property.check <| property {
    //    let! x = GenX.auto<int>

    //    let get _ = ValueNone
    //    let d = Binding.twoWayOptValidate(get, fail2, (fail: _ -> Result<_,_>)) |> getValidationData

    //    test <@ isNull (d.Get x) @>
    //  }


    //[<Fact>]
    //let ``when final set receives a non-null value, original get receives ValueSome`` () =
    //  Property.check <| property {
    //    let! m = GenX.auto<int>
    //    let! p = GenX.auto<string>

    //    let set (p: string voption) (m: int) = p |> ValueOption.map ((+) (string m))
    //    let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> Result<_,_>)) |> getValidationData

    //    test <@ d.Set (box p) m |> unbox = set (ValueSome p) m @>
    //  }


    //[<Fact>]
    //let ``when final set receives null, original get receives ValueNone`` () =
    //  Property.check <| property {
    //    let! m = GenX.auto<int>

    //    let set (p: string voption) (m: int) = p |> ValueOption.map ((+) (string m))
    //    let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> Result<_,_>)) |> getValidationData

    //    test <@ d.Set null m |> unbox = set ValueNone m @>
    //  }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then [] else [ err ]
        let d = Binding.twoWayOptValidate((fail: _ -> _ voption), fail2, validate) |> getValidationData

        test <@ d.Validate x |> unbox = validate x @>
      }



  module option_setModel_validateVoption =


    //[<Fact>]
    //let ``when original get returns Some, final get returns the inner value`` () =
    //  Property.check <| property {
    //    let! x = GenX.auto<int>

    //    let get = string >> Some
    //    let d = Binding.twoWayOptValidate(get, fail2, (fail: _ -> _ voption)) |> getValidationData

    //    test <@ d.Get x |> unbox = (get x).Value @>
    //  }


    //[<Fact>]
    //let ``when original get returns None, final get returns null`` () =
    //  Property.check <| property {
    //    let! x = GenX.auto<int>

    //    let get _ = None
    //    let d = Binding.twoWayOptValidate(get, fail2, (fail: _ -> _ voption)) |> getValidationData

    //    test <@ isNull (d.Get x) @>
    //  }


    //[<Fact>]
    //let ``when final set receives a non-null value, original get receives Some`` () =
    //  Property.check <| property {
    //    let! m = GenX.auto<int>
    //    let! p = GenX.auto<string>

    //    let set (p: string option) (m: int) = p |> Option.map ((+) (string m))
    //    let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> _ voption)) |> getValidationData

    //    test <@ d.Set (box p) m |> unbox = set (Some p) m @>
    //  }


    //[<Fact>]
    //let ``when final set receives null, original get receives None`` () =
    //  Property.check <| property {
    //    let! m = GenX.auto<int>

    //    let set (p: string option) (m: int) = p |> Option.map ((+) (string m))
    //    let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> _ voption)) |> getValidationData

    //    test <@ d.Set null m |> unbox = set None m @>
    //  }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then [ err ] else []
        let d = Binding.twoWayOptValidate((fail: _ -> _ option), fail2, validate) |> getValidationData

        test <@ d.Validate x |> unbox = validate x @>
      }



  module option_setModel_validateOption =


    //[<Fact>]
    //let ``when original get returns Some, final get returns the inner value`` () =
    //  Property.check <| property {
    //    let! x = GenX.auto<int>

    //    let get = string >> Some
    //    let d = Binding.twoWayOptValidate(get, fail2, (fail: _ -> _ option)) |> getValidationData

    //    test <@ d.Get x |> unbox = (get x).Value @>
    //  }


    //[<Fact>]
    //let ``when original get returns None, final get returns null`` () =
    //  Property.check <| property {
    //    let! x = GenX.auto<int>

    //    let get _ = None
    //    let d = Binding.twoWayOptValidate(get, fail2, (fail: _ -> _ option)) |> getValidationData

    //    test <@ isNull (d.Get x) @>
    //  }


    //[<Fact>]
    //let ``when final set receives a non-null value, original get receives Some`` () =
    //  Property.check <| property {
    //    let! m = GenX.auto<int>
    //    let! p = GenX.auto<string>

    //    let set (p: string option) (m: int) = p |> Option.map ((+) (string m))
    //    let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> _ option)) |> getValidationData

    //    test <@ d.Set (box p) m |> unbox = set (Some p) m @>
    //  }


    //[<Fact>]
    //let ``when final set receives null, original get receives None`` () =
    //  Property.check <| property {
    //    let! m = GenX.auto<int>

    //    let set (p: string option) (m: int) = p |> Option.map ((+) (string m))
    //    let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> _ option)) |> getValidationData

    //    test <@ d.Set null m |> unbox = set None m @>
    //  }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then [ err ] else []
        let d = Binding.twoWayOptValidate((fail: _ -> _ option), fail2, validate) |> getValidationData

        test <@ d.Validate x |> unbox = validate x @>
      }



  module option_setModel_validateResult =


    //[<Fact>]
    //let ``when original get returns Some, final get returns the inner value`` () =
    //  Property.check <| property {
    //    let! x = GenX.auto<int>

    //    let get = string >> Some
    //    let d = Binding.twoWayOptValidate(get, fail2, (fail: _ -> Result<_,_>)) |> getValidationData

    //    test <@ d.Get x |> unbox = (get x).Value @>
    //  }


    //[<Fact>]
    //let ``when original get returns None, final get returns null`` () =
    //  Property.check <| property {
    //    let! x = GenX.auto<int>

    //    let get _ = None
    //    let d = Binding.twoWayOptValidate(get, fail2, (fail: _ -> Result<_,_>)) |> getValidationData

    //    test <@ isNull (d.Get x) @>
    //  }


    //[<Fact>]
    //let ``when final set receives a non-null value, original get receives Some`` () =
    //  Property.check <| property {
    //    let! m = GenX.auto<int>
    //    let! p = GenX.auto<string>

    //    let set (p: string option) (m: int) = p |> Option.map ((+) (string m))
    //    let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> Result<_,_>)) |> getValidationData

    //    test <@ d.Set (box p) m |> unbox = set (Some p) m @>
    //  }


    //[<Fact>]
    //let ``when final set receives null, original get receives None`` () =
    //  Property.check <| property {
    //    let! m = GenX.auto<int>

    //    let set (p: string option) (m: int) = p |> Option.map ((+) (string m))
    //    let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> Result<_,_>)) |> getValidationData

    //    test <@ d.Set null m |> unbox = set None m @>
    //  }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then [] else [ err ]
        let d = Binding.twoWayOptValidate((fail: _ -> _ option), fail2, validate) |> getValidationData

        test <@ d.Validate x |> unbox = validate x @>
      }



  module voption_noSetModel_validateVoption =


    //[<Fact>]
    //let ``when original get returns ValueSome, final get returns the inner value`` () =
    //  Property.check <| property {
    //    let! x = GenX.auto<int>

    //    let get = string >> ValueSome
    //    let d = Binding.twoWayOptValidate(get, (fail: _ -> int), (fail: _ -> _ voption)) |> getValidationData

    //    test <@ d.Get x |> unbox = (get x).Value @>
    //  }


    //[<Fact>]
    //let ``when original get returns ValueNone, final get returns null`` () =
    //  Property.check <| property {
    //    let! x = GenX.auto<int>

    //    let get _ = ValueNone
    //    let d = Binding.twoWayOptValidate(get, (fail: _ -> int), (fail: _ -> _ voption)) |> getValidationData

    //    test <@ isNull (d.Get x) @>
    //  }


    //[<Fact>]
    //let ``when final set receives a non-null value, original get receives ValueSome`` () =
    //  Property.check <| property {
    //    let! m = GenX.auto<int>
    //    let! p = GenX.auto<string>

    //    let set (p: string voption) = p |> ValueOption.map (fun x -> x + x)
    //    let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> _ voption)) |> getValidationData

    //    test <@ d.Set (box p) m |> unbox = set (ValueSome p) @>
    //  }


    //[<Fact>]
    //let ``when final set receives null, original get receives ValueNone`` () =
    //  Property.check <| property {
    //    let! m = GenX.auto<int>

    //    let set (p: string voption) = p |> ValueOption.map (fun x -> x + x)
    //    let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> _ voption)) |> getValidationData

    //    test <@ d.Set null m |> unbox = set ValueNone @>
    //  }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then [ err ] else []
        let d = Binding.twoWayOptValidate((fail: _ -> _ voption), (fail: _ -> int), validate) |> getValidationData

        test <@ d.Validate x |> unbox = validate x @>
      }



  module voption_noSetModel_validateOption =


    //[<Fact>]
    //let ``when original get returns ValueSome, final get returns the inner value`` () =
    //  Property.check <| property {
    //    let! x = GenX.auto<int>

    //    let get = string >> ValueSome
    //    let d = Binding.twoWayOptValidate(get, (fail: _ -> int), (fail: _ -> _ option)) |> getValidationData

    //    test <@ d.Get x |> unbox = (get x).Value @>
    //  }


    //[<Fact>]
    //let ``when original get returns ValueNone, final get returns null`` () =
    //  Property.check <| property {
    //    let! x = GenX.auto<int>

    //    let get _ = ValueNone
    //    let d = Binding.twoWayOptValidate(get, (fail: _ -> int), (fail: _ -> _ option)) |> getValidationData

    //    test <@ isNull (d.Get x) @>
    //  }


    //[<Fact>]
    //let ``when final set receives a non-null value, original get receives ValueSome`` () =
    //  Property.check <| property {
    //    let! m = GenX.auto<int>
    //    let! p = GenX.auto<string>

    //    let set (p: string voption) = p |> ValueOption.map (fun x -> x + x)
    //    let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> _ option)) |> getValidationData

    //    test <@ d.Set (box p) m |> unbox = set (ValueSome p) @>
    //  }


    //[<Fact>]
    //let ``when final set receives null, original get receives ValueNone`` () =
    //  Property.check <| property {
    //    let! m = GenX.auto<int>

    //    let set (p: string voption) = p |> ValueOption.map (fun x -> x + x)
    //    let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> _ option)) |> getValidationData

    //    test <@ d.Set null m |> unbox = set ValueNone @>
    //  }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then [ err ] else []
        let d = Binding.twoWayOptValidate((fail: _ -> _ voption), (fail: _ -> int), validate) |> getValidationData

        test <@ d.Validate x |> unbox = validate x @>
      }



  module voption_noSetModel_validateResult =


    //[<Fact>]
    //let ``when original get returns ValueSome, final get returns the inner value`` () =
    //  Property.check <| property {
    //    let! x = GenX.auto<int>

    //    let get = string >> ValueSome
    //    let d = Binding.twoWayOptValidate(get, (fail: _ -> int), (fail: _ -> Result<_,_>)) |> getValidationData

    //    test <@ d.Get x |> unbox = (get x).Value @>
    //  }


    //[<Fact>]
    //let ``when original get returns ValueNone, final get returns null`` () =
    //  Property.check <| property {
    //    let! x = GenX.auto<int>

    //    let get _ = ValueNone
    //    let d = Binding.twoWayOptValidate(get, (fail: _ -> int), (fail: _ -> Result<_,_>)) |> getValidationData

    //    test <@ isNull (d.Get x) @>
    //  }


    //[<Fact>]
    //let ``when final set receives a non-null value, original get receives ValueSome`` () =
    //  Property.check <| property {
    //    let! m = GenX.auto<int>
    //    let! p = GenX.auto<string>

    //    let set (p: string voption) = p |> ValueOption.map (fun x -> x + x)
    //    let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> Result<_,_>)) |> getValidationData

    //    test <@ d.Set (box p) m |> unbox = set (ValueSome p) @>
    //  }


    //[<Fact>]
    //let ``when final set receives null, original get receives ValueNone`` () =
    //  Property.check <| property {
    //    let! m = GenX.auto<int>

    //    let set (p: string voption) = p |> ValueOption.map (fun x -> x + x)
    //    let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> Result<_,_>)) |> getValidationData

    //    test <@ d.Set null m |> unbox = set ValueNone @>
    //  }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then [] else [ err ]
        let d = Binding.twoWayOptValidate((fail: _ -> _ voption), (fail: _ -> int), validate) |> getValidationData

        test <@ d.Validate x |> unbox = validate x @>
      }



  module option_noSetModel_validateVoption =


    //[<Fact>]
    //let ``when original get returns Some, final get returns the inner value`` () =
    //  Property.check <| property {
    //    let! x = GenX.auto<int>

    //    let get = string >> Some
    //    let d = Binding.twoWayOptValidate(get, (fail: _ -> int), (fail: _ -> _ voption)) |> getValidationData

    //    test <@ d.Get x |> unbox = (get x).Value @>
    //  }


    //[<Fact>]
    //let ``when original get returns None, final get returns null`` () =
    //  Property.check <| property {
    //    let! x = GenX.auto<int>

    //    let get _ = None
    //    let d = Binding.twoWayOptValidate(get, (fail: _ -> int), (fail: _ -> _ voption)) |> getValidationData

    //    test <@ isNull (d.Get x) @>
    //  }


    //[<Fact>]
    //let ``when final set receives a non-null value, original get receives Some`` () =
    //  Property.check <| property {
    //    let! m = GenX.auto<int>
    //    let! p = GenX.auto<string>

    //    let set (p: string option) = p |> Option.map (fun x -> x + x)
    //    let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> _ voption)) |> getValidationData

    //    test <@ d.Set (box p) m |> unbox = set (Some p) @>
    //  }


    //[<Fact>]
    //let ``when final set receives null, original get receives None`` () =
    //  Property.check <| property {
    //    let! m = GenX.auto<int>

    //    let set (p: string option) = p |> Option.map (fun x -> x + x)
    //    let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> _ voption)) |> getValidationData

    //    test <@ d.Set null m |> unbox = set None @>
    //  }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then [ err ] else []
        let d = Binding.twoWayOptValidate((fail: _ -> _ option), (fail: _ -> int), validate) |> getValidationData

        test <@ d.Validate x |> unbox = validate x @>
      }



  module option_noSetModel_validateOption =


    //[<Fact>]
    //let ``when original get returns Some, final get returns the inner value`` () =
    //  Property.check <| property {
    //    let! x = GenX.auto<int>

    //    let get = string >> Some
    //    let d = Binding.twoWayOptValidate(get, (fail: _ -> int), (fail: _ -> _ option)) |> getValidationData

    //    test <@ d.Get x |> unbox = (get x).Value @>
    //  }


    //[<Fact>]
    //let ``when original get returns None, final get returns null`` () =
    //  Property.check <| property {
    //    let! x = GenX.auto<int>

    //    let get _ = None
    //    let d = Binding.twoWayOptValidate(get, (fail: _ -> int), (fail: _ -> _ option)) |> getValidationData

    //    test <@ isNull (d.Get x) @>
    //  }


    //[<Fact>]
    //let ``when final set receives a non-null value, original get receives Some`` () =
    //  Property.check <| property {
    //    let! m = GenX.auto<int>
    //    let! p = GenX.auto<string>

    //    let set (p: string option) = p |> Option.map (fun x -> x + x)
    //    let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> _ option)) |> getValidationData

    //    test <@ d.Set (box p) m |> unbox = set (Some p) @>
    //  }


    //[<Fact>]
    //let ``when final set receives null, original get receives None`` () =
    //  Property.check <| property {
    //    let! m = GenX.auto<int>

    //    let set (p: string option) = p |> Option.map (fun x -> x + x)
    //    let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> _ option)) |> getValidationData

    //    test <@ d.Set null m |> unbox = set None @>
    //  }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then [ err ] else []
        let d = Binding.twoWayOptValidate((fail: _ -> _ option), (fail: _ -> int), validate) |> getValidationData

        test <@ d.Validate x |> unbox = validate x @>
      }



  module option_noSetModel_validateResult =


    //[<Fact>]
    //let ``when original get returns Some, final get returns the inner value`` () =
    //  Property.check <| property {
    //    let! x = GenX.auto<int>

    //    let get = string >> Some
    //    let d = Binding.twoWayOptValidate(get, (fail: _ -> int), (fail: _ -> Result<_,_>)) |> getValidationData

    //    test <@ d.Get x |> unbox = (get x).Value @>
    //  }


    //[<Fact>]
    //let ``when original get returns None, final get returns null`` () =
    //  Property.check <| property {
    //    let! x = GenX.auto<int>

    //    let get _ = None
    //    let d = Binding.twoWayOptValidate(get, (fail: _ -> int), (fail: _ -> Result<_,_>)) |> getValidationData

    //    test <@ isNull (d.Get x) @>
    //  }


    //[<Fact>]
    //let ``when final set receives a non-null value, original get receives Some`` () =
    //  Property.check <| property {
    //    let! m = GenX.auto<int>
    //    let! p = GenX.auto<string>

    //    let set (p: string option) = p |> Option.map (fun x -> x + x)
    //    let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> Result<_,_>)) |> getValidationData

    //    test <@ d.Set (box p) m |> unbox = set (Some p) @>
    //  }


    //[<Fact>]
    //let ``when final set receives null, original get receives None`` () =
    //  Property.check <| property {
    //    let! m = GenX.auto<int>

    //    let set (p: string option) = p |> Option.map (fun x -> x + x)
    //    let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> Result<_,_>)) |> getValidationData

    //    test <@ d.Set null m |> unbox = set None @>
    //  }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then [] else [ err ]
        let d = Binding.twoWayOptValidate((fail: _ -> _ option), (fail: _ -> int), validate) |> getValidationData

        test <@ d.Validate x |> unbox = validate x @>
      }



module cmd =


  module model =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.cmd(fail)
        test <@ binding.Name = bindingName @>
      }



  module noModel =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.cmd(obj())
        test <@ binding.Name = bindingName @>
      }



module cmdIf =


  module explicitCanExec_model =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.cmdIf(fail, fail, id)
        test <@ binding.Name = bindingName @>
      }


  module explicitCanExec_noModel =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.cmdIf(obj(), fail)
        test <@ binding.Name = bindingName @>
      }



  module voption =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.cmdIf(fail: _ -> _ voption)
        test <@ binding.Name = bindingName @>
      }



  module option =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.cmdIf(fail: _ -> _ option)
        test <@ binding.Name = bindingName @>
      }



  module result =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.cmdIf(fail: _ -> Result<_,_>)
        test <@ binding.Name = bindingName @>
      }



module cmdParam =


  module model =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.cmdParam(fail2)
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final exec returns value from original exec wrapped in ValueSome`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let exec (p: obj) (m: int) = unbox p + string m
        let d = Binding.cmdParam(exec) |> getCmdData

        test <@ d.Exec (box p) m = (exec p m |> ValueSome) @>
      }


    [<Fact>]
    let ``canExec always returns true`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>
        let d = Binding.cmdParam(fail2) |> getCmdData
        test <@ d.CanExec (box p) m = true @>
      }


    [<Fact>]
    let ``autoRequery is false`` () =
      Property.check <| property {
        let d = Binding.cmdParam(fail2) |> getCmdData
        test <@ d.AutoRequery = false @>
      }



  module noModel =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.cmdParam(fail: obj -> obj)
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final exec returns original value wrapped in ValueSome`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let exec (p: obj) = string p
        let d = Binding.cmdParam(exec) |> getCmdData

        test <@ d.Exec (box p) m = (exec p |> ValueSome) @>
      }


    [<Fact>]
    let ``canExec always returns true`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>
        let d = Binding.cmdParam(fail: obj -> obj) |> getCmdData
        test <@ d.CanExec (box p) m = true @>
      }


    [<Fact>]
    let ``autoRequery is false`` () =
      Property.check <| property {
        let d = Binding.cmdParam(fail: obj -> obj) |> getCmdData
        test <@ d.AutoRequery = false @>
      }



module cmdParamIf =


  module explicitCanExec_model =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.cmdParamIf(fail, fail)
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final exec returns value from original exec wrapped in ValueSome`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let exec (p: obj) (m: int) = unbox p + string m
        let d = Binding.cmdParamIf(exec, fail) |> getCmdData

        test <@ d.Exec (box p) m = (exec p m |> ValueSome) @>
      }


    [<Fact>]
    let ``final canExec returns value from original canExec`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let canExec (p: obj) m = (unbox<string> p).Length + m > 0
        let d = Binding.cmdParamIf(fail, canExec) |> getCmdData

        test <@ d.CanExec (box p) m = canExec p m @>
      }


    [<Fact>]
    let ``final autoRequery defaults to false`` () =
      Property.check <| property {
        let d = Binding.cmdParamIf(fail, fail) |> getCmdData
        test <@ d.AutoRequery = false @>
      }


    [<Fact>]
    let ``final autoRequery equals original uiBoundCmdParam`` () =
      Property.check <| property {
        let! uiBoundCmdParam = GenX.auto<bool>
        let d = Binding.cmdParamIf(fail, fail, uiBoundCmdParam = uiBoundCmdParam) |> getCmdData
        test <@ d.AutoRequery = uiBoundCmdParam @>
      }


  module voption_model =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.cmdParamIf(fail2: _ -> _ -> _ voption)
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final exec returns value from original exec`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let exec (p: obj) m = (p :?> string).Length + m |> ValueSome |> ValueOption.filter (fun x -> x > 0)
        let d = Binding.cmdParamIf(exec) |> getCmdData

        test <@ d.Exec (box p) m = exec p m @>
      }


    [<Fact>]
    let ``final canExec returns true if original exec returns ValueSome`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let exec (p: obj) m = (p :?> string).Length + m |> ValueSome
        let d = Binding.cmdParamIf(exec) |> getCmdData

        test <@ d.CanExec (box p) m = true @>
      }


    [<Fact>]
    let ``final canExec returns false if original exec returns ValueNone`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let exec (_: obj) _ = ValueNone
        let d = Binding.cmdParamIf(exec) |> getCmdData

        test <@ d.CanExec (box p) m = false @>
      }


    [<Fact>]
    let ``final autoRequery defaults to false`` () =
      Property.check <| property {
        let d = Binding.cmdParamIf((fail2: _ -> _ -> _ voption)) |> getCmdData
        test <@ d.AutoRequery = false @>
      }


    [<Fact>]
    let ``final autoRequery equals original uiBoundCmdParam`` () =
      Property.check <| property {
        let! uiBoundCmdParam = GenX.auto<bool>
        let d = Binding.cmdParamIf((fail2: _ -> _ -> _ voption), uiBoundCmdParam = uiBoundCmdParam) |> getCmdData
        test <@ d.AutoRequery = uiBoundCmdParam @>
      }



  module option_model =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.cmdParamIf(fail2: _ -> _ -> _ option)
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final exec returns value from original exec converted to ValueOption`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let exec (p: obj) m = (p :?> string).Length + m |> Some |> Option.filter (fun x -> x > 0)
        let d = Binding.cmdParamIf(exec) |> getCmdData

        test <@ d.Exec (box p) m = (exec p m |> ValueOption.ofOption) @>
      }


    [<Fact>]
    let ``final canExec returns true if original exec returns Some`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let exec (p: obj) m = (p :?> string).Length + m |> Some
        let d = Binding.cmdParamIf(exec) |> getCmdData

        test <@ d.CanExec (box p) m = true @>
      }


    [<Fact>]
    let ``final canExec returns false if original exec returns None`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let exec (_: obj) _ = None
        let d = Binding.cmdParamIf(exec) |> getCmdData

        test <@ d.CanExec (box p) m = false @>
      }


    [<Fact>]
    let ``final autoRequery defaults to false`` () =
      Property.check <| property {
        let d = Binding.cmdParamIf((fail2: _ -> _ -> _ option)) |> getCmdData
        test <@ d.AutoRequery = false @>
      }


    [<Fact>]
    let ``final autoRequery equals original uiBoundCmdParam`` () =
      Property.check <| property {
        let! uiBoundCmdParam = GenX.auto<bool>
        let d = Binding.cmdParamIf((fail2: _ -> _ -> _ option), uiBoundCmdParam = uiBoundCmdParam) |> getCmdData
        test <@ d.AutoRequery = uiBoundCmdParam @>
      }



  module result_model =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.cmdParamIf(fail2: _ -> _ -> Result<_,_>)
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final exec returns Ok value from original exec converted to ValueOption`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let exec (p: obj) m =
          let x = (p :?> string).Length + m
          if x > 0 then Ok x else Error (string x)
        let d = Binding.cmdParamIf(exec) |> getCmdData

        test <@ d.Exec (box p) m = (exec p m |> ValueOption.ofOk) @>
      }


    [<Fact>]
    let ``final canExec returns true if original exec returns Ok`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let exec (p: obj) m = (p :?> string).Length + m |> Ok
        let d = Binding.cmdParamIf(exec) |> getCmdData

        test <@ d.CanExec (box p) m = true @>
      }


    [<Fact>]
    let ``final canExec returns false if original exec returns Error`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>
        let! err = GenX.auto<byte>

        let exec (_: obj) _ = Error err
        let d = Binding.cmdParamIf(exec) |> getCmdData

        test <@ d.CanExec (box p) m = false @>
      }


    [<Fact>]
    let ``final autoRequery defaults to false`` () =
      Property.check <| property {
        let d = Binding.cmdParamIf((fail2: _ -> _ -> Result<_,_>)) |> getCmdData
        test <@ d.AutoRequery = false @>
      }


    [<Fact>]
    let ``final autoRequery equals original uiBoundCmdParam`` () =
      Property.check <| property {
        let! uiBoundCmdParam = GenX.auto<bool>
        let d = Binding.cmdParamIf((fail2: _ -> _ -> Result<_,_>), uiBoundCmdParam = uiBoundCmdParam) |> getCmdData
        test <@ d.AutoRequery = uiBoundCmdParam @>
      }



  module explicitCanExec_noModel =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.cmdParamIf((fail: obj -> obj), fail)
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final exec returns value from original exec wrapped in ValueSome`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let exec (p: obj) = (unbox<string> p).Length
        let d = Binding.cmdParamIf(exec, fail) |> getCmdData

        test <@ d.Exec (box p) m = (exec p |> ValueSome) @>
      }


    [<Fact>]
    let ``final canExec returns value from original canExec`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let canExec (p: obj) = (unbox<string> p).Length + m > 0
        let d = Binding.cmdParamIf(fail, canExec) |> getCmdData

        test <@ d.CanExec (box p) m = canExec p @>
      }


    [<Fact>]
    let ``final autoRequery defaults to false`` () =
      Property.check <| property {
        let d = Binding.cmdParamIf((fail: obj -> obj), fail) |> getCmdData
        test <@ d.AutoRequery = false @>
      }


    [<Fact>]
    let ``final autoRequery equals original uiBoundCmdParam`` () =
      Property.check <| property {
        let! uiBoundCmdParam = GenX.auto<bool>
        let d = Binding.cmdParamIf((fail: obj -> obj), fail, uiBoundCmdParam = uiBoundCmdParam) |> getCmdData
        test <@ d.AutoRequery = uiBoundCmdParam @>
      }


  module voption_noModel =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.cmdParamIf(fail: _ -> _ voption)
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final exec returns value from original exec`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let exec (p: obj) = (p :?> string).Length |> ValueSome |> ValueOption.filter (fun x -> x > 0)
        let d = Binding.cmdParamIf(exec) |> getCmdData

        test <@ d.Exec (box p) m = exec p @>
      }


    [<Fact>]
    let ``final canExec returns true if original exec returns ValueSome`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let exec (p: obj) = (p :?> string).Length |> ValueSome
        let d = Binding.cmdParamIf(exec) |> getCmdData

        test <@ d.CanExec (box p) m = true @>
      }


    [<Fact>]
    let ``final canExec returns false if original exec returns ValueNone`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let exec (_: obj) = ValueNone
        let d = Binding.cmdParamIf(exec) |> getCmdData

        test <@ d.CanExec (box p) m = false @>
      }


    [<Fact>]
    let ``final autoRequery defaults to false`` () =
      Property.check <| property {
        let d = Binding.cmdParamIf((fail: _ -> _ voption)) |> getCmdData
        test <@ d.AutoRequery = false @>
      }


    [<Fact>]
    let ``final autoRequery equals original uiBoundCmdParam`` () =
      Property.check <| property {
        let! uiBoundCmdParam = GenX.auto<bool>
        let d = Binding.cmdParamIf((fail: _ -> _ voption), uiBoundCmdParam = uiBoundCmdParam) |> getCmdData
        test <@ d.AutoRequery = uiBoundCmdParam @>
      }



  module option_noModel =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.cmdParamIf(fail: _ -> _ option)
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final exec returns value from original exec converted to ValueOption`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let exec (p: obj) = (p :?> string).Length |> Some |> Option.filter (fun x -> x > 0)
        let d = Binding.cmdParamIf(exec) |> getCmdData

        test <@ d.Exec (box p) m = (exec p |> ValueOption.ofOption) @>
      }


    [<Fact>]
    let ``final canExec returns true if original exec returns Some`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let exec (p: obj) = (p :?> string).Length |> Some
        let d = Binding.cmdParamIf(exec) |> getCmdData

        test <@ d.CanExec (box p) m = true @>
      }


    [<Fact>]
    let ``final canExec returns false if original exec returns None`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let exec (_: obj) = None
        let d = Binding.cmdParamIf(exec) |> getCmdData

        test <@ d.CanExec (box p) m = false @>
      }


    [<Fact>]
    let ``final autoRequery defaults to false`` () =
      Property.check <| property {
        let d = Binding.cmdParamIf((fail: _ -> _ option)) |> getCmdData
        test <@ d.AutoRequery = false @>
      }


    [<Fact>]
    let ``final autoRequery equals original uiBoundCmdParam`` () =
      Property.check <| property {
        let! uiBoundCmdParam = GenX.auto<bool>
        let d = Binding.cmdParamIf((fail: _ -> _ option), uiBoundCmdParam = uiBoundCmdParam) |> getCmdData
        test <@ d.AutoRequery = uiBoundCmdParam @>
      }



  module result_noModel =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.cmdParamIf(fail: _ -> Result<_,_>)
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final exec returns Ok value from original exec converted to ValueOption`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let exec (p: obj) =
          let x = (p :?> string).Length
          if x > 0 then Ok x else Error (string x)
        let d = Binding.cmdParamIf(exec) |> getCmdData

        test <@ d.Exec (box p) m = (exec p |> ValueOption.ofOk) @>
      }


    [<Fact>]
    let ``final canExec returns true if original exec returns Ok`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let exec (p: obj) = (p :?> string).Length |> Ok
        let d = Binding.cmdParamIf(exec) |> getCmdData

        test <@ d.CanExec (box p) m = true @>
      }


    [<Fact>]
    let ``final canExec returns false if original exec returns Error`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>
        let! err = GenX.auto<byte>

        let exec (_: obj) = Error err
        let d = Binding.cmdParamIf(exec) |> getCmdData

        test <@ d.CanExec (box p) m = false @>
      }


    [<Fact>]
    let ``final autoRequery defaults to false`` () =
      Property.check <| property {
        let d = Binding.cmdParamIf((fail: _ -> Result<_,_>)) |> getCmdData
        test <@ d.AutoRequery = false @>
      }


    [<Fact>]
    let ``final autoRequery equals original uiBoundCmdParam`` () =
      Property.check <| property {
        let! uiBoundCmdParam = GenX.auto<bool>
        let d = Binding.cmdParamIf((fail: _ -> Result<_,_>), uiBoundCmdParam = uiBoundCmdParam) |> getCmdData
        test <@ d.AutoRequery = uiBoundCmdParam @>
      }



module subModel =


  module noToMsg_noToBindingModel =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.subModel(fail, fail)
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final getModel combines main model and return value of getSubModel, and wraps in ValueSome`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let getSubModel = string<int>
        let d = Binding.subModel(getSubModel, fail) |> getSubModelData
        test <@ d.GetModel x = ((x, getSubModel x) |> box |> ValueSome) @>
      }


    [<Fact>]
    let ``final toMsg simply unboxes`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! x = GenX.auto<int>
        let d = Binding.subModel((fun _ -> 0), fail) |> getSubModelData
        test <@ d.ToMsg m (box x) = x @>
      }



  module toMsg_noToBindingModel =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.subModel(fail, fail, fail)
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final getModel combines main model and return value of getSubModel, and wraps in ValueSome`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let getSubModel = string<int>
        let d = Binding.subModel(getSubModel, fail, fail) |> getSubModelData
        test <@ d.GetModel x = ((x, getSubModel x) |> box |> ValueSome) @>
      }


    [<Fact>]
    let ``final toMsg returns value from original toMsg`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! x = GenX.auto<int>

        let toMsg = string<int>
        let d = Binding.subModel((fun _ -> 0), toMsg, fail) |> getSubModelData

        test <@ d.ToMsg m (box x) = toMsg x @>
      }


  module toMsg_toBindingModel =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.subModel(fail, fail, fail, fail)
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final getModel calls toBindingModel on main model and return value of getSubModel, and wraps in ValueSome`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let getSubModel = string<int>
        let toBindingModel (m: int, s: string) = m + s.Length
        let d = Binding.subModel(getSubModel, toBindingModel, fail, fail) |> getSubModelData
        test <@ d.GetModel x = ((x, getSubModel x) |> toBindingModel |> box |> ValueSome) @>
      }


    [<Fact>]
    let ``final toMsg returns value from original toMsg`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! x = GenX.auto<int>

        let toMsg = string<int>
        let d = Binding.subModel((fun _ -> 0), (fun _ -> 0), toMsg, fail) |> getSubModelData

        test <@ d.ToMsg m (box x) = toMsg x @>
      }



module subModelOpt =


  module voption_noToMsg_noToBindingModel =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.subModelOpt((fail: _ -> _ voption), fail)
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final getModel combines main model and inner return value of getSubModel if ValueSome`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let getSubModel = string >> ValueSome
        let d = Binding.subModelOpt(getSubModel, fail) |> getSubModelData
        test <@ d.GetModel x = ((x, (getSubModel x).Value) |> box |> ValueSome) @>
      }


    [<Fact>]
    let ``final getModel returns ValueNone if getSubModel returns ValueNone`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let getSubModel (_: int) : string voption = ValueNone
        let d = Binding.subModelOpt(getSubModel, fail) |> getSubModelData
        test <@ d.GetModel x = ValueNone @>
      }


    [<Fact>]
    let ``final toMsg simply unboxes`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! x = GenX.auto<int>
        let d = Binding.subModelOpt((fun _ -> ValueSome 0), fail) |> getSubModelData
        test <@ d.ToMsg m (box x) = x @>
      }



  module option_noToMsg_noToBindingModel =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.subModelOpt((fail: _ -> _ option), fail)
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final getModel combines main model and inner return value of getSubModel if ValueSome`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let getSubModel = string >> Some
        let d = Binding.subModelOpt(getSubModel, fail) |> getSubModelData
        test <@ d.GetModel x = ((x, (getSubModel x).Value) |> box |> ValueSome) @>
      }


    [<Fact>]
    let ``final getModel returns ValueNone if getSubModel returns ValueNone`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let getSubModel (_: int) : string option = None
        let d = Binding.subModelOpt(getSubModel, fail) |> getSubModelData
        test <@ d.GetModel x = ValueNone @>
      }


    [<Fact>]
    let ``final toMsg simply unboxes`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! x = GenX.auto<int>
        let d = Binding.subModelOpt((fun _ -> Some 0), fail) |> getSubModelData
        test <@ d.ToMsg m (box x) = x @>
      }


  module voption_toMsg_noToBindingModel =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.subModelOpt((fail: _ -> _ voption), fail, fail)
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final getModel combines main model and inner return value of getSubModel if ValueSome`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let getSubModel = string >> ValueSome
        let d = Binding.subModelOpt(getSubModel, fail, fail) |> getSubModelData
        test <@ d.GetModel x = ((x, (getSubModel x).Value) |> box |> ValueSome) @>
      }


    [<Fact>]
    let ``final getModel returns ValueNone if getSubModel returns ValueNone`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let getSubModel (_: int) : string voption = ValueNone
        let d = Binding.subModelOpt(getSubModel, fail, fail) |> getSubModelData
        test <@ d.GetModel x = ValueNone @>
      }


    [<Fact>]
    let ``final toMsg returns value from original toMsg`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! x = GenX.auto<int>

        let toMsg = string<int>
        let d = Binding.subModelOpt((fun _ -> ValueSome 0), toMsg, fail) |> getSubModelData

        test <@ d.ToMsg m (box x) = toMsg x @>
      }



  module option_toMsg_noToBindingModel =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.subModelOpt((fail: _ -> _ option), fail, fail)
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final getModel combines main model and inner return value of getSubModel if ValueSome`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let getSubModel = string >> Some
        let d = Binding.subModelOpt(getSubModel, fail, fail) |> getSubModelData
        test <@ d.GetModel x = ((x, (getSubModel x).Value) |> box |> ValueSome) @>
      }


    [<Fact>]
    let ``final getModel returns ValueNone if getSubModel returns ValueNone`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let getSubModel (_: int) : string option = None
        let d = Binding.subModelOpt(getSubModel, fail, fail) |> getSubModelData
        test <@ d.GetModel x = ValueNone @>
      }


    [<Fact>]
    let ``final toMsg returns value from original toMsg`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! x = GenX.auto<int>

        let toMsg = string<int>
        let d = Binding.subModelOpt((fun _ -> Some 0), toMsg, fail) |> getSubModelData

        test <@ d.ToMsg m (box x) = toMsg x @>
      }


  module voption_toMsg_toBindingModel =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.subModelOpt((fail: _ -> _ voption), fail, fail, fail)
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final getModel calls toBindingModel on main model and inner return value of getSubModel if ValueSome`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let getSubModel = string >> ValueSome
        let toBindingModel (m: int, s: string) = m + s.Length
        let d = Binding.subModelOpt(getSubModel, toBindingModel, fail, fail) |> getSubModelData
        test <@ d.GetModel x = ((x, (getSubModel x).Value) |> toBindingModel |> box |> ValueSome) @>
      }


    [<Fact>]
    let ``final getModel returns ValueNone if getSubModel returns ValueNone`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let getSubModel (_: int) : string voption = ValueNone
        let d = Binding.subModelOpt(getSubModel, fail, fail, fail) |> getSubModelData
        test <@ d.GetModel x = ValueNone @>
      }


    [<Fact>]
    let ``final toMsg returns value from original toMsg`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! x = GenX.auto<int>

        let toMsg = string<int>
        let d = Binding.subModelOpt((fun _ -> ValueSome 0), (fun _ -> ValueSome 0), toMsg, fail) |> getSubModelData

        test <@ d.ToMsg m (box x) = toMsg x @>
      }



  module option_toMsg_toBindingModel =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.subModelOpt((fail: _ -> _ option), fail, fail, fail)
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final getModel calls toBindingModel on main model and inner return value of getSubModel if Some`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let getSubModel = string >> Some
        let toBindingModel (m: int, s: string) = m + s.Length
        let d = Binding.subModelOpt(getSubModel, toBindingModel, fail, fail) |> getSubModelData
        test <@ d.GetModel x = ((x, (getSubModel x).Value) |> toBindingModel |> box |> ValueSome) @>
      }


    [<Fact>]
    let ``final getModel returns ValueNone if getSubModel returns ValueNone`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let getSubModel (_: int) : string option = None
        let d = Binding.subModelOpt(getSubModel, fail, fail, fail) |> getSubModelData
        test <@ d.GetModel x = ValueNone @>
      }


    [<Fact>]
    let ``final toMsg returns value from original toMsg`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! x = GenX.auto<int>

        let toMsg = string<int>
        let d = Binding.subModelOpt((fun _ -> Some 0), (fun _ -> Some 0), toMsg, fail) |> getSubModelData

        test <@ d.ToMsg m (box x) = toMsg x @>
      }



module subModelSeqKeyed =


  module noToMsg_noToBindingModel =

    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.subModelSeq(fail, fail, fail)
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final getModels returns tuples of the items returned by getSubModels and the main model`` () =
      Property.check <| property {
        let! m = GenX.auto<string>
        let getSubModels : string -> char list = Seq.toList
        let d = Binding.subModelSeq(getSubModels, fail, fail) |> getSubModelSeqKeyedData
        test <@ d.GetSubModels m |> Seq.map unbox |> Seq.toList = (m |> getSubModels |> List.map (fun s -> m, s)) @>
      }


    [<Fact>]
    let ``final getId returns the ID of each element in final getModels`` () =
      Property.check <| property {
        let! m = GenX.auto<string>
        let getSubModels : string -> char list = Seq.toList
        let getId : char -> string = string
        let d = Binding.subModelSeq(getSubModels, getId, fail) |> getSubModelSeqKeyedData
        test <@ d.GetSubModels m |> Seq.map d.GetId |> Seq.map unbox |> Seq.toList = (m |> getSubModels |> List.map getId) @>
      }


  module toMsg_noToBindingModel =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.subModelSeq(fail, fail, fail, fail)
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final getModels returns tuples of the items returned by getSubModels and the main model`` () =
      Property.check <| property {
        let! m = GenX.auto<string>
        let getSubModels : string -> char list = Seq.toList
        let d = Binding.subModelSeq(getSubModels, fail, fail, fail) |> getSubModelSeqKeyedData
        test <@ d.GetSubModels m |> Seq.map unbox |> Seq.toList = (m |> getSubModels |> List.map (fun s -> m, s)) @>
      }


    [<Fact>]
    let ``final getId returns the ID of each element in final getModels`` () =
      Property.check <| property {
        let! m = GenX.auto<string>
        let getSubModels : string -> char list = Seq.toList
        let getId : char -> string = string
        let d = Binding.subModelSeq(getSubModels, getId, fail, fail) |> getSubModelSeqKeyedData
        test <@ d.GetSubModels m |> Seq.map d.GetId |> Seq.map unbox |> Seq.toList = (m |> getSubModels |> List.map getId) @>
      }


  module toMsg_toBindingModel =


      [<Fact>]
      let ``sets the correct binding name`` () =
        Property.check <| property {
          let! bindingName = GenX.auto<string>
          let binding = bindingName |> Binding.subModelSeq(fail, fail, fail, fail, fail)
          test <@ binding.Name = bindingName @>
        }


      [<Fact>]
      let ``final getModels returns output of toBindingModel called with tuples of the items returned by getSubModels and the main model`` () =
        Property.check <| property {
          let! m = GenX.auto<string>
          let getSubModels : string -> char list = Seq.toList
          let toBindingModel (m: string, c: char) = (m + string c).Length
          let d = Binding.subModelSeq(getSubModels, toBindingModel, fail, fail, fail) |> getSubModelSeqKeyedData
          test <@ d.GetSubModels m |> Seq.map unbox |> Seq.toList = (m |> getSubModels |> List.map (fun s -> toBindingModel (m, s))) @>
        }


      [<Fact>]
      let ``final getId returns the ID of each element in final getModels`` () =
        Property.check <| property {
          let! m = GenX.auto<string>
          let getSubModels : string -> char list = Seq.toList
          let toBindingModel (m: string, c: char) = (m + string c).Length
          let getId i = i * 2
          let d = Binding.subModelSeq(getSubModels, toBindingModel, getId, fail, fail) |> getSubModelSeqKeyedData
          test <@ d.GetSubModels m |> Seq.map d.GetId |> Seq.map unbox |> Seq.toList = (m |> getSubModels |> List.map (fun s -> toBindingModel (m, s)) |> List.map getId) @>
        }



module subModelSelectedItem =


  module voption_setModel =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.subModelSelectedItem("", (fail: _ -> _ voption), fail2)
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``sets the correct subModelSeqBindingName`` () =
      Property.check <| property {
        let! name = GenX.auto<string>
        let d = Binding.subModelSelectedItem(name, (fail: _ -> _ voption), fail2) |> getSubModelSelectedItemData
        test <@ d.SubModelSeqBindingName = name @>
      }


    [<Fact>]
    let ``final get returns value from original get`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! useNone = Gen.bool
        let get (x: int) = if useNone then ValueNone else x |> string |> ValueSome
        let d = Binding.subModelSelectedItem("", get, fail2) |> getSubModelSelectedItemData
        test <@ d.Get x |> ValueOption.map unbox<string> = get x @>
      }


    [<Fact>]
    let ``final set returns value from original set`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string voption>
        let get _ = ValueNone
        let set (p: string voption) m = p |> ValueOption.map (fun p -> p.Length + m |> string)
        let d = Binding.subModelSelectedItem("", get, set) |> getSubModelSelectedItemData
        test <@ d.Set (p |> ValueOption.map box) m = set p m @>
      }


  module option_setModel =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.subModelSelectedItem("", (fail: _ -> _ option), fail2)
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``sets the correct subModelSeqBindingName`` () =
      Property.check <| property {
        let! name = GenX.auto<string>
        let d = Binding.subModelSelectedItem(name, (fail: _ -> _ option), fail2) |> getSubModelSelectedItemData
        test <@ d.SubModelSeqBindingName = name @>
      }


    [<Fact>]
    let ``final get returns value from original get converted to ValueOption`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! useNone = Gen.bool
        let get (x: int) = if useNone then None else x |> string |> Some
        let d = Binding.subModelSelectedItem("", get, fail2) |> getSubModelSelectedItemData
        test <@ d.Get x |> ValueOption.map unbox = (get x |> ValueOption.ofOption) @>
      }


    [<Fact>]
    let ``final set returns value from original set`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string option>
        let get _ = None
        let set (p: string option) m = p |> Option.map (fun p -> p.Length + m |> string)
        let d = Binding.subModelSelectedItem("", get, set) |> getSubModelSelectedItemData
        test <@ d.Set (p |> Option.map box |> ValueOption.ofOption) m = set p m @>
      }


  module voption_noSetModel =


      [<Fact>]
      let ``sets the correct binding name`` () =
        Property.check <| property {
          let! bindingName = GenX.auto<string>
          let binding = bindingName |> Binding.subModelSelectedItem("", (fail: _ -> _ voption), (fail: _ -> obj))
          test <@ binding.Name = bindingName @>
        }


      [<Fact>]
      let ``sets the correct subModelSeqBindingName`` () =
        Property.check <| property {
          let! name = GenX.auto<string>
          let d = Binding.subModelSelectedItem(name, (fail: _ -> _ voption), (fail: _ -> obj)) |> getSubModelSelectedItemData
          test <@ d.SubModelSeqBindingName = name @>
        }


      [<Fact>]
      let ``final get returns value from original get`` () =
        Property.check <| property {
          let! x = GenX.auto<int>
          let! useNone = Gen.bool
          let get (x: int) = if useNone then ValueNone else x |> string |> ValueSome
          let d = Binding.subModelSelectedItem("", get, (fail: _ -> obj)) |> getSubModelSelectedItemData
          test <@ d.Get x |> ValueOption.map unbox = get x @>
        }


      [<Fact>]
      let ``final set returns value from original set`` () =
        Property.check <| property {
          let! m = GenX.auto<int>
          let! p = GenX.auto<string voption>
          let get _ = ValueNone
          let set (p: string voption) = p |> ValueOption.map (fun p -> p.Length |> string)
          let d = Binding.subModelSelectedItem("", get, set) |> getSubModelSelectedItemData
          test <@ d.Set (p |> ValueOption.map box) m = set p @>
        }


  module option_noSetModel =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.subModelSelectedItem("", (fail: _ -> _ option), (fail: _ -> obj))
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``sets the correct subModelSeqBindingName`` () =
      Property.check <| property {
        let! name = GenX.auto<string>
        let d = Binding.subModelSelectedItem(name, (fail: _ -> _ option), (fail: _ -> obj)) |> getSubModelSelectedItemData
        test <@ d.SubModelSeqBindingName = name @>
      }


    [<Fact>]
    let ``final get returns value from original get converted to ValueOption`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! useNone = Gen.bool
        let get (x: int) = if useNone then None else x |> string |> Some
        let d = Binding.subModelSelectedItem("", get, (fail: _ -> obj)) |> getSubModelSelectedItemData
        test <@ d.Get x |> ValueOption.map unbox = (get x |> ValueOption.ofOption) @>
      }


    [<Fact>]
    let ``final set returns value from original set`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string option>
        let get _ = None
        let set (p: string option) = p |> Option.map (fun p -> p.Length |> string)
        let d = Binding.subModelSelectedItem("", get, set) |> getSubModelSelectedItemData
        test <@ d.Set (p |> Option.map box |> ValueOption.ofOption) m = set p @>
      }



module sorting =

  [<Fact>]
    let ``SubModelSelectedItemData sorted last`` () =
      Property.check <| property {
        let! s = GenX.auto<string>
        let data =
          [ SubModelSelectedItemData { Get = fail; Set = fail2; SubModelSeqBindingName = s }
            SubModelSeqKeyedData { GetSubModels = fail; GetId = fail; GetBindings = fail; ToMsg = fail }
            SubModelSelectedItemData { Get = fail; Set = fail2; SubModelSeqBindingName = s }
          ] |> List.map BaseBindingData
        let sorted = data |> List.sortWith (SubModelSelectedItemLast().CompareBindingDatas())
        match sorted with
        | [_; BaseBindingData (SubModelSelectedItemData _); BaseBindingData (SubModelSelectedItemData _)] -> ()
        | _ -> failwith "SubModelSelectedItemData was not sorted last"
      }
