namespace Elmish.WPF.Tests.BindingTests

open Xunit
open Hedgehog
open Swensen.Unquote
open Elmish.WPF


[<AutoOpen>]
module Helpers =

  let fail _ = failwith "Placeholder function was invoked"
  let fail2 _ _ = failwith "Placeholder function was invoked"

  let internal getOneWayData f =
    match f "" with
    | { Data = OneWayData d } -> d
    | _ -> failwith "Incorrect binding"

  let internal getOneWayLazyData f =
    match f "" with
    | { Data = OneWayLazyData d } -> d
    | _ -> failwith "Incorrect binding"

  let internal getOneWaySeqLazyData f =
    match f "" with
    | { Data = OneWaySeqLazyData d } -> d
    | _ -> failwith "Incorrect binding"

  let internal getTwoWayData f =
    match f "" with
    | { Data = TwoWayData d } -> d
    | _ -> failwith "Incorrect binding"

  let internal getTwoWayValidateData f =
    match f "" with
    | { Data = TwoWayValidateData d } -> d
    | _ -> failwith "Incorrect binding"

  let internal getCmdData f =
    match f "" with
    | { Data = CmdData d } -> d
    | _ -> failwith "Incorrect binding"

  let internal getCmdParamData f =
    match f "" with
    | { Data = CmdParamData d } -> d
    | _ -> failwith "Incorrect binding"

  let internal getSubModelData f =
    match f "" with
    | { Data = SubModelData d } -> d
    | _ -> failwith "Incorrect binding"

  let internal getSubModelSeqData f =
    match f "" with
    | { Data = SubModelSeqData d } -> d
    | _ -> failwith "Incorrect binding"

  let internal getSubModelSelectedItemData f =
    match f "" with
    | { Data = SubModelSelectedItemData d } -> d
    | _ -> failwith "Incorrect binding"



module oneWay =


  [<Fact>]
  let ``sets the correct binding name`` () =
    Property.check <| property {
      let! bindingName = GenX.auto<string>
      let binding = bindingName |> Binding.oneWay(fail)
      test <@ binding.Name = bindingName @>
    }


  [<Fact>]
  let ``final get returns value from original get`` () =
    Property.check <| property {
      let! x = GenX.auto<int>

      let get = string
      let d = Binding.oneWay(get) |> getOneWayData

      test <@ d.Get x |> unbox = get x @>
    }


module oneWayOpt =


  module option =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.oneWayOpt((fail: _ -> _ option))
        test <@ binding.Name = bindingName @>
      }


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

        let get m = None
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



module oneWayLazy =


  [<Fact>]
  let ``sets the correct binding name`` () =
    Property.check <| property {
      let! bindingName = GenX.auto<string>
      let binding = bindingName |> Binding.oneWayLazy(fail, fail2, fail)
      test <@ binding.Name = bindingName @>
    }


  [<Fact>]
  let ``final get returns value from original get`` () =
    Property.check <| property {
      let! x = GenX.auto<int>

      let get = string
      let d = Binding.oneWayLazy(get, fail2, fail) |> getOneWayLazyData

      test <@ d.Get x |> unbox = get x @>
    }


  [<Fact>]
  let ``final equals returns value from original equals`` () =
    Property.check <| property {
      let! x = GenX.auto<int>
      let! y = GenX.auto<int>

      let equals : int -> int -> bool = (=)
      let d = Binding.oneWayLazy(fail, equals, fail) |> getOneWayLazyData

      test <@ d.Equals (box x) (box y) = equals x y @>
    }


  [<Fact>]
  let ``final map returns value from original map`` () =
    Property.check <| property {
      let! x = GenX.auto<int>

      let map = string
      let d = Binding.oneWayLazy(fail, fail2, map) |> getOneWayLazyData

      test <@ d.Map (box x) |> unbox = map x @>
    }



module oneWayOptLazy =


  module option =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.oneWayOptLazy(fail, fail2, (fail: _ -> _ option))
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final get returns value from original get`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get = string
        let d = Binding.oneWayOptLazy(get, fail2, (fail: _ -> _ option)) |> getOneWayLazyData

        test <@ d.Get x |> unbox = get x @>
      }


    [<Fact>]
    let ``final equals returns value from original equals`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! y = GenX.auto<int>

        let equals : int -> int -> bool = (=)
        let d = Binding.oneWayOptLazy(fail, equals, (fail: _ -> _ option)) |> getOneWayLazyData

        test <@ d.Equals (box x) (box y) = equals x y @>
      }


    [<Fact>]
    let ``when original map returns Some, final map returns the inner value`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let map = string >> Some
        let d = Binding.oneWayOptLazy(fail, fail2, map) |> getOneWayLazyData

        test <@ d.Map (box x) |> unbox = (map x).Value @>
      }


    [<Fact>]
    let ``when original map returns None, final map returns null`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let map _ = None
        let d = Binding.oneWayOptLazy(fail, fail2, map) |> getOneWayLazyData

        test <@ isNull (d.Map (box x)) @>
      }



  module voption =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.oneWayOptLazy(fail, fail2, (fail: _ -> _ voption))
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final get returns value from original get`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get = string
        let d = Binding.oneWayOptLazy(get, fail2, (fail: _ -> _ voption)) |> getOneWayLazyData

        test <@ d.Get x |> unbox = get x @>
      }


    [<Fact>]
    let ``final equals returns value from original equals`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! y = GenX.auto<int>

        let equals : int -> int -> bool = (=)
        let d = Binding.oneWayOptLazy(fail, equals, (fail: _ -> _ voption)) |> getOneWayLazyData

        test <@ d.Equals (box x) (box y) = equals x y @>
      }


    [<Fact>]
    let ``when original map returns ValueSome, final map returns the inner value`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let map = string >> ValueSome
        let d = Binding.oneWayOptLazy(fail, fail2, map) |> getOneWayLazyData

        test <@ d.Map (box x) |> unbox = (map x).Value @>
      }


    [<Fact>]
    let ``when original map returns ValueNone, final map returns null`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let map _ = ValueNone
        let d = Binding.oneWayOptLazy(fail, fail2, map) |> getOneWayLazyData

        test <@ isNull (d.Map (box x)) @>
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
  let ``final get passes through model`` () =
    Property.check <| property {
      let! x = GenX.auto<int>

      let d = Binding.oneWaySeq(fail, fail2, fail) |> getOneWaySeqLazyData

      test <@ d.Get x |> unbox = x @>
    }


  [<Fact>]
  let ``final map returns value from original get`` () =
    Property.check <| property {
      let! x = GenX.auto<string>

      let get : string -> char list = Seq.toList
      let d = Binding.oneWaySeq(get, fail2, fail) |> getOneWaySeqLazyData

      test <@ d.Map (box x) |> Seq.map unbox |> Seq.toList = get x @>
    }


  [<Fact>]
  let ``final equals returns false`` () =
    Property.check <| property {
      let! x = GenX.auto<int>
      let! y = GenX.auto<int>

      let d = Binding.oneWaySeq(fail, fail2, fail) |> getOneWaySeqLazyData

      test <@ d.Equals (box x) (box y) = false @>
    }


  [<Fact>]
  let ``final getId returns value from original getId`` () =
    Property.check <| property {
      let! x = GenX.auto<int>

      let getId = string
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

      let get = string
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

      let getId = string
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
        let binding = bindingName |> Binding.twoWay(fail, fail2)
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final get returns value from original get`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get = string
        let d = Binding.twoWay(get, fail2) |> getTwoWayData

        test <@ d.Get x |> unbox = get x @>
      }


    [<Fact>]
    let ``final set returns value from original set`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let set (p: string) (m: int) = p + string m
        let d = Binding.twoWay(fail, set) |> getTwoWayData

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

        let get = string
        let d = Binding.twoWay(get, (fail: string -> int)) |> getTwoWayData

        test <@ d.Get x |> unbox = get x @>
      }


    [<Fact>]
    let ``final set returns value from original set`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let set (p: string) = p + p
        let d = Binding.twoWay(fail, set) |> getTwoWayData

        test <@ d.Set (box p) m |> unbox = set p @>
      }



module twoWayOpt =


  module option_setModel =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.twoWayOpt((fail: _ -> _ option), fail2)
        test <@ binding.Name = bindingName @>
      }


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

        let get m = None
        let d = Binding.twoWayOpt(get, fail2) |> getTwoWayData

        test <@ isNull (d.Get x) @>
      }


    [<Fact>]
    let ``when final set receives a non-null value, original get receives Some`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let set (p: string option) (m: int) = p |> Option.map ((+) (string m))
        let d = Binding.twoWayOpt(fail, set) |> getTwoWayData

        test <@ d.Set (box p) m |> unbox = set (Some p) m @>
      }


    [<Fact>]
    let ``when final set receives null, original get receives None`` () =
      Property.check <| property {
        let! m = GenX.auto<int>

        let set (p: string option) (m: int) = p |> Option.map ((+) (string m))
        let d = Binding.twoWayOpt(fail, set) |> getTwoWayData

        test <@ d.Set null m |> unbox = set None m @>
      }



  module voption_setModel =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.twoWayOpt((fail: _ -> _ voption), fail2)
        test <@ binding.Name = bindingName @>
      }


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

        let get m = ValueNone
        let d = Binding.twoWayOpt(get, fail2) |> getTwoWayData

        test <@ isNull (d.Get x) @>
      }


    [<Fact>]
    let ``when final set receives a non-null value, original get receives ValueSome`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let set (p: string voption) (m: int) = p |> ValueOption.map ((+) (string m))
        let d = Binding.twoWayOpt(fail, set) |> getTwoWayData

        test <@ d.Set (box p) m |> unbox = set (ValueSome p) m @>
      }


    [<Fact>]
    let ``when final set receives null, original get receives ValueNone`` () =
      Property.check <| property {
        let! m = GenX.auto<int>

        let set (p: string voption) (m: int) = p |> ValueOption.map ((+) (string m))
        let d = Binding.twoWayOpt(fail, set) |> getTwoWayData

        test <@ d.Set null m |> unbox = set ValueNone m @>
      }



  module option_noSetModel =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.twoWayOpt((fail: _ -> _ option), (fail: _ option -> int))
        test <@ binding.Name = bindingName @>
      }


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

        let get m = None
        let d = Binding.twoWayOpt(get, (fail: _ option -> int)) |> getTwoWayData

        test <@ isNull (d.Get x) @>
      }


    [<Fact>]
    let ``when final set receives a non-null value, original get receives Some`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let set (p: string option) = p |> Option.map ((+) (string m))
        let d = Binding.twoWayOpt(fail, set) |> getTwoWayData

        test <@ d.Set (box p) m |> unbox = set (Some p) @>
      }


    [<Fact>]
    let ``when final set receives null, original get receives None`` () =
      Property.check <| property {
        let! m = GenX.auto<int>

        let set (p: string option) = p |> Option.map ((+) (string m))
        let d = Binding.twoWayOpt(fail, set) |> getTwoWayData

        test <@ d.Set null m |> unbox = set None @>
      }



  module voption_noSetModel =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.twoWayOpt((fail: _ -> _ voption), (fail: _ voption -> int))
        test <@ binding.Name = bindingName @>
      }


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

        let get m = ValueNone
        let d = Binding.twoWayOpt(get, (fail: _ voption -> int)) |> getTwoWayData

        test <@ isNull (d.Get x) @>
      }


    [<Fact>]
    let ``when final set receives a non-null value, original get receives ValueSome`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let set (p: string voption) = p |> ValueOption.map ((+) (string m))
        let d = Binding.twoWayOpt(fail, set) |> getTwoWayData

        test <@ d.Set (box p) m |> unbox = set (ValueSome p) @>
      }


    [<Fact>]
    let ``when final set receives null, original get receives ValueNone`` () =
      Property.check <| property {
        let! m = GenX.auto<int>

        let set (p: string voption) = p |> ValueOption.map ((+) (string m))
        let d = Binding.twoWayOpt(fail, set) |> getTwoWayData

        test <@ d.Set null m |> unbox = set ValueNone @>
      }



module twoWayValidate =


  module setModel_validateVoption =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.twoWayValidate(fail, fail2, (fail: _ -> _ voption))
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final get returns value from original get`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get = string
        let d = Binding.twoWayValidate(get, fail2, (fail: _ -> _ voption)) |> getTwoWayValidateData

        test <@ d.Get x |> unbox = get x @>
      }


    [<Fact>]
    let ``final set returns value from original set`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let set (p: string) (m: int) = p + string m
        let d = Binding.twoWayValidate(fail, set, (fail: _ -> _ voption)) |> getTwoWayValidateData

        test <@ d.Set (box p) m |> unbox = set p m @>
      }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then ValueSome err else ValueNone
        let d = Binding.twoWayValidate(fail, fail2, validate) |> getTwoWayValidateData

        test <@ d.Validate x |> unbox = validate x @>
      }



  module setModel_validateOption =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.twoWayValidate(fail, fail2, (fail: _ -> _ option))
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final get returns value from original get`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get = string
        let d = Binding.twoWayValidate(get, fail2, (fail: _ -> _ option)) |> getTwoWayValidateData

        test <@ d.Get x |> unbox = get x @>
      }


    [<Fact>]
    let ``final set returns value from original set`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let set (p: string) (m: int) = p + string m
        let d = Binding.twoWayValidate(fail, set, (fail: _ -> _ option)) |> getTwoWayValidateData

        test <@ d.Set (box p) m |> unbox = set p m @>
      }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then Some err else None
        let d = Binding.twoWayValidate(fail, fail2, validate) |> getTwoWayValidateData

        test <@ d.Validate x |> unbox = (validate x |> ValueOption.ofOption) @>
      }



  module setModel_validateResult =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.twoWayValidate(fail, fail2, (fail: _ -> Result<_,_>))
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final get returns value from original get`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get = string
        let d = Binding.twoWayValidate(get, fail2, (fail: _ -> Result<_,_>)) |> getTwoWayValidateData

        test <@ d.Get x |> unbox = get x @>
      }


    [<Fact>]
    let ``final set returns value from original set`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let set (p: string) (m: int) = p + string m
        let d = Binding.twoWayValidate(fail, set, (fail: _ -> Result<_,_>)) |> getTwoWayValidateData

        test <@ d.Set (box p) m |> unbox = set p m @>
      }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! ok = GenX.auto<byte>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then Ok ok else Error err
        let d = Binding.twoWayValidate(fail, fail2, validate) |> getTwoWayValidateData

        test <@ d.Validate x |> unbox = (validate x |> ValueOption.ofError) @>
      }



  module noSetModel_validateVoption =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.twoWayValidate(fail, (fail: string -> int), (fail: _ -> _ voption))
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final get returns value from original get`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get = string
        let d = Binding.twoWayValidate(get, (fail: string -> int), (fail: _ -> _ voption)) |> getTwoWayValidateData

        test <@ d.Get x |> unbox = get x @>
      }


    [<Fact>]
    let ``final set returns value from original set`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let set (p: string) = p + p
        let d = Binding.twoWayValidate(fail, set, (fail: _ -> _ voption)) |> getTwoWayValidateData

        test <@ d.Set (box p) m |> unbox = set p @>
      }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then ValueSome err else ValueNone
        let d = Binding.twoWayValidate(fail, (fail: string -> int), validate) |> getTwoWayValidateData

        test <@ d.Validate x |> unbox = validate x @>
      }



  module noSetModel_validateOption =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.twoWayValidate(fail, (fail: string -> int), (fail: _ -> _ option))
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final get returns value from original get`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get = string
        let d = Binding.twoWayValidate(get, (fail: string -> int), (fail: _ -> _ option)) |> getTwoWayValidateData

        test <@ d.Get x |> unbox = get x @>
      }


    [<Fact>]
    let ``final set returns value from original set`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let set (p: string) = p + p
        let d = Binding.twoWayValidate(fail, set, (fail: _ -> _ option)) |> getTwoWayValidateData

        test <@ d.Set (box p) m |> unbox = set p @>
      }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then Some err else None
        let d = Binding.twoWayValidate(fail, (fail: string -> int), validate) |> getTwoWayValidateData

        test <@ d.Validate x |> unbox = (validate x |> ValueOption.ofOption) @>
      }



  module noSetModel_validateResult =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.twoWayValidate(fail, (fail: string -> int), (fail: _ -> Result<_,_>))
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final get returns value from original get`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get = string
        let d = Binding.twoWayValidate(get, (fail: string -> int), (fail: _ -> Result<_,_>)) |> getTwoWayValidateData

        test <@ d.Get x |> unbox = get x @>
      }


    [<Fact>]
    let ``final set returns value from original set`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let set (p: string) = p + p
        let d = Binding.twoWayValidate(fail, set, (fail: _ -> Result<_,_>)) |> getTwoWayValidateData

        test <@ d.Set (box p) m |> unbox = set p @>
      }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! ok = GenX.auto<byte>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then Ok ok else Error err
        let d = Binding.twoWayValidate(fail, (fail: string -> int), validate) |> getTwoWayValidateData

        test <@ d.Validate x |> unbox = (validate x |> ValueOption.ofError) @>
      }



module twoWayOptValidate =


  module voption_setModel_validateVoption =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.twoWayOptValidate((fail: _ -> _ voption), fail2, (fail: _ -> _ voption))
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``when original get returns ValueSome, final get returns the inner value`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get = string >> ValueSome
        let d = Binding.twoWayOptValidate(get, fail2, (fail: _ -> _ voption)) |> getTwoWayValidateData

        test <@ d.Get x |> unbox = (get x).Value @>
      }


    [<Fact>]
    let ``when original get returns ValueNone, final get returns null`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get m = ValueNone
        let d = Binding.twoWayOptValidate(get, fail2, (fail: _ -> _ voption)) |> getTwoWayValidateData

        test <@ isNull (d.Get x) @>
      }


    [<Fact>]
    let ``when final set receives a non-null value, original get receives ValueSome`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let set (p: string voption) (m: int) = p |> ValueOption.map ((+) (string m))
        let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> _ voption)) |> getTwoWayValidateData

        test <@ d.Set (box p) m |> unbox = set (ValueSome p) m @>
      }


    [<Fact>]
    let ``when final set receives null, original get receives ValueNone`` () =
      Property.check <| property {
        let! m = GenX.auto<int>

        let set (p: string voption) (m: int) = p |> ValueOption.map ((+) (string m))
        let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> _ voption)) |> getTwoWayValidateData

        test <@ d.Set null m |> unbox = set ValueNone m @>
      }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then ValueSome err else ValueNone
        let d = Binding.twoWayOptValidate((fail: _ -> _ voption), fail2, validate) |> getTwoWayValidateData

        test <@ d.Validate x |> unbox = validate x @>
      }



  module voption_setModel_validateOption =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.twoWayOptValidate((fail: _ -> _ voption), fail2, (fail: _ -> _ option))
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``when original get returns ValueSome, final get returns the inner value`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get = string >> ValueSome
        let d = Binding.twoWayOptValidate(get, fail2, (fail: _ -> _ option)) |> getTwoWayValidateData

        test <@ d.Get x |> unbox = (get x).Value @>
      }


    [<Fact>]
    let ``when original get returns ValueNone, final get returns null`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get m = ValueNone
        let d = Binding.twoWayOptValidate(get, fail2, (fail: _ -> _ option)) |> getTwoWayValidateData

        test <@ isNull (d.Get x) @>
      }


    [<Fact>]
    let ``when final set receives a non-null value, original get receives ValueSome`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let set (p: string voption) (m: int) = p |> ValueOption.map ((+) (string m))
        let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> _ option)) |> getTwoWayValidateData

        test <@ d.Set (box p) m |> unbox = set (ValueSome p) m @>
      }


    [<Fact>]
    let ``when final set receives null, original get receives ValueNone`` () =
      Property.check <| property {
        let! m = GenX.auto<int>

        let set (p: string voption) (m: int) = p |> ValueOption.map ((+) (string m))
        let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> _ option)) |> getTwoWayValidateData

        test <@ d.Set null m |> unbox = set ValueNone m @>
      }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then Some err else None
        let d = Binding.twoWayOptValidate((fail: _ -> _ voption), fail2, validate) |> getTwoWayValidateData

        test <@ d.Validate x |> unbox = (validate x |> ValueOption.ofOption) @>
      }



  module voption_setModel_validateResult =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.twoWayOptValidate((fail: _ -> _ voption), fail2, (fail: _ -> Result<_,_>))
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``when original get returns ValueSome, final get returns the inner value`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get = string >> ValueSome
        let d = Binding.twoWayOptValidate(get, fail2, (fail: _ -> Result<_,_>)) |> getTwoWayValidateData

        test <@ d.Get x |> unbox = (get x).Value @>
      }


    [<Fact>]
    let ``when original get returns ValueNone, final get returns null`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get m = ValueNone
        let d = Binding.twoWayOptValidate(get, fail2, (fail: _ -> Result<_,_>)) |> getTwoWayValidateData

        test <@ isNull (d.Get x) @>
      }


    [<Fact>]
    let ``when final set receives a non-null value, original get receives ValueSome`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let set (p: string voption) (m: int) = p |> ValueOption.map ((+) (string m))
        let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> Result<_,_>)) |> getTwoWayValidateData

        test <@ d.Set (box p) m |> unbox = set (ValueSome p) m @>
      }


    [<Fact>]
    let ``when final set receives null, original get receives ValueNone`` () =
      Property.check <| property {
        let! m = GenX.auto<int>

        let set (p: string voption) (m: int) = p |> ValueOption.map ((+) (string m))
        let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> Result<_,_>)) |> getTwoWayValidateData

        test <@ d.Set null m |> unbox = set ValueNone m @>
      }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! ok = GenX.auto<byte>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then Ok ok else Error err
        let d = Binding.twoWayOptValidate((fail: _ -> _ voption), fail2, validate) |> getTwoWayValidateData

        test <@ d.Validate x |> unbox = (validate x |> ValueOption.ofError) @>
      }



  module option_setModel_validateVoption =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.twoWayOptValidate((fail: _ -> _ option), fail2, (fail: _ -> _ voption))
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``when original get returns Some, final get returns the inner value`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get = string >> Some
        let d = Binding.twoWayOptValidate(get, fail2, (fail: _ -> _ voption)) |> getTwoWayValidateData

        test <@ d.Get x |> unbox = (get x).Value @>
      }


    [<Fact>]
    let ``when original get returns None, final get returns null`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get m = None
        let d = Binding.twoWayOptValidate(get, fail2, (fail: _ -> _ voption)) |> getTwoWayValidateData

        test <@ isNull (d.Get x) @>
      }


    [<Fact>]
    let ``when final set receives a non-null value, original get receives Some`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let set (p: string option) (m: int) = p |> Option.map ((+) (string m))
        let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> _ voption)) |> getTwoWayValidateData

        test <@ d.Set (box p) m |> unbox = set (Some p) m @>
      }


    [<Fact>]
    let ``when final set receives null, original get receives None`` () =
      Property.check <| property {
        let! m = GenX.auto<int>

        let set (p: string option) (m: int) = p |> Option.map ((+) (string m))
        let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> _ voption)) |> getTwoWayValidateData

        test <@ d.Set null m |> unbox = set None m @>
      }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then ValueSome err else ValueNone
        let d = Binding.twoWayOptValidate((fail: _ -> _ option), fail2, validate) |> getTwoWayValidateData

        test <@ d.Validate x |> unbox = validate x @>
      }



  module option_setModel_validateOption =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.twoWayOptValidate((fail: _ -> _ option), fail2, (fail: _ -> _ option))
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``when original get returns Some, final get returns the inner value`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get = string >> Some
        let d = Binding.twoWayOptValidate(get, fail2, (fail: _ -> _ option)) |> getTwoWayValidateData

        test <@ d.Get x |> unbox = (get x).Value @>
      }


    [<Fact>]
    let ``when original get returns None, final get returns null`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get m = None
        let d = Binding.twoWayOptValidate(get, fail2, (fail: _ -> _ option)) |> getTwoWayValidateData

        test <@ isNull (d.Get x) @>
      }


    [<Fact>]
    let ``when final set receives a non-null value, original get receives Some`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let set (p: string option) (m: int) = p |> Option.map ((+) (string m))
        let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> _ option)) |> getTwoWayValidateData

        test <@ d.Set (box p) m |> unbox = set (Some p) m @>
      }


    [<Fact>]
    let ``when final set receives null, original get receives None`` () =
      Property.check <| property {
        let! m = GenX.auto<int>

        let set (p: string option) (m: int) = p |> Option.map ((+) (string m))
        let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> _ option)) |> getTwoWayValidateData

        test <@ d.Set null m |> unbox = set None m @>
      }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then Some err else None
        let d = Binding.twoWayOptValidate((fail: _ -> _ option), fail2, validate) |> getTwoWayValidateData

        test <@ d.Validate x |> unbox = (validate x |> ValueOption.ofOption) @>
      }



  module option_setModel_validateResult =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.twoWayOptValidate((fail: _ -> _ option), fail2, (fail: _ -> Result<_,_>))
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``when original get returns Some, final get returns the inner value`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get = string >> Some
        let d = Binding.twoWayOptValidate(get, fail2, (fail: _ -> Result<_,_>)) |> getTwoWayValidateData

        test <@ d.Get x |> unbox = (get x).Value @>
      }


    [<Fact>]
    let ``when original get returns None, final get returns null`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get m = None
        let d = Binding.twoWayOptValidate(get, fail2, (fail: _ -> Result<_,_>)) |> getTwoWayValidateData

        test <@ isNull (d.Get x) @>
      }


    [<Fact>]
    let ``when final set receives a non-null value, original get receives Some`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let set (p: string option) (m: int) = p |> Option.map ((+) (string m))
        let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> Result<_,_>)) |> getTwoWayValidateData

        test <@ d.Set (box p) m |> unbox = set (Some p) m @>
      }


    [<Fact>]
    let ``when final set receives null, original get receives None`` () =
      Property.check <| property {
        let! m = GenX.auto<int>

        let set (p: string option) (m: int) = p |> Option.map ((+) (string m))
        let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> Result<_,_>)) |> getTwoWayValidateData

        test <@ d.Set null m |> unbox = set None m @>
      }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! ok = GenX.auto<byte>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then Ok ok else Error err
        let d = Binding.twoWayOptValidate((fail: _ -> _ option), fail2, validate) |> getTwoWayValidateData

        test <@ d.Validate x |> unbox = (validate x |> ValueOption.ofError) @>
      }



  module voption_noSetModel_validateVoption =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.twoWayOptValidate((fail: _ -> _ voption), (fail: _ -> int), (fail: _ -> _ voption))
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``when original get returns ValueSome, final get returns the inner value`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get = string >> ValueSome
        let d = Binding.twoWayOptValidate(get, (fail: _ -> int), (fail: _ -> _ voption)) |> getTwoWayValidateData

        test <@ d.Get x |> unbox = (get x).Value @>
      }


    [<Fact>]
    let ``when original get returns ValueNone, final get returns null`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get m = ValueNone
        let d = Binding.twoWayOptValidate(get, (fail: _ -> int), (fail: _ -> _ voption)) |> getTwoWayValidateData

        test <@ isNull (d.Get x) @>
      }


    [<Fact>]
    let ``when final set receives a non-null value, original get receives ValueSome`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let set (p: string voption) = p |> ValueOption.map (fun x -> x + x)
        let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> _ voption)) |> getTwoWayValidateData

        test <@ d.Set (box p) m |> unbox = set (ValueSome p) @>
      }


    [<Fact>]
    let ``when final set receives null, original get receives ValueNone`` () =
      Property.check <| property {
        let! m = GenX.auto<int>

        let set (p: string voption) = p |> ValueOption.map (fun x -> x + x)
        let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> _ voption)) |> getTwoWayValidateData

        test <@ d.Set null m |> unbox = set ValueNone @>
      }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then ValueSome err else ValueNone
        let d = Binding.twoWayOptValidate((fail: _ -> _ voption), (fail: _ -> int), validate) |> getTwoWayValidateData

        test <@ d.Validate x |> unbox = validate x @>
      }



  module voption_noSetModel_validateOption =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.twoWayOptValidate((fail: _ -> _ voption), (fail: _ -> int), (fail: _ -> _ option))
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``when original get returns ValueSome, final get returns the inner value`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get = string >> ValueSome
        let d = Binding.twoWayOptValidate(get, (fail: _ -> int), (fail: _ -> _ option)) |> getTwoWayValidateData

        test <@ d.Get x |> unbox = (get x).Value @>
      }


    [<Fact>]
    let ``when original get returns ValueNone, final get returns null`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get m = ValueNone
        let d = Binding.twoWayOptValidate(get, (fail: _ -> int), (fail: _ -> _ option)) |> getTwoWayValidateData

        test <@ isNull (d.Get x) @>
      }


    [<Fact>]
    let ``when final set receives a non-null value, original get receives ValueSome`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let set (p: string voption) = p |> ValueOption.map (fun x -> x + x)
        let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> _ option)) |> getTwoWayValidateData

        test <@ d.Set (box p) m |> unbox = set (ValueSome p) @>
      }


    [<Fact>]
    let ``when final set receives null, original get receives ValueNone`` () =
      Property.check <| property {
        let! m = GenX.auto<int>

        let set (p: string voption) = p |> ValueOption.map (fun x -> x + x)
        let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> _ option)) |> getTwoWayValidateData

        test <@ d.Set null m |> unbox = set ValueNone @>
      }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then Some err else None
        let d = Binding.twoWayOptValidate((fail: _ -> _ voption), (fail: _ -> int), validate) |> getTwoWayValidateData

        test <@ d.Validate x |> unbox = (validate x |> ValueOption.ofOption) @>
      }



  module voption_noSetModel_validateResult =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.twoWayOptValidate((fail: _ -> _ voption), (fail: _ -> int), (fail: _ -> Result<_,_>))
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``when original get returns ValueSome, final get returns the inner value`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get = string >> ValueSome
        let d = Binding.twoWayOptValidate(get, (fail: _ -> int), (fail: _ -> Result<_,_>)) |> getTwoWayValidateData

        test <@ d.Get x |> unbox = (get x).Value @>
      }


    [<Fact>]
    let ``when original get returns ValueNone, final get returns null`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get m = ValueNone
        let d = Binding.twoWayOptValidate(get, (fail: _ -> int), (fail: _ -> Result<_,_>)) |> getTwoWayValidateData

        test <@ isNull (d.Get x) @>
      }


    [<Fact>]
    let ``when final set receives a non-null value, original get receives ValueSome`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let set (p: string voption) = p |> ValueOption.map (fun x -> x + x)
        let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> Result<_,_>)) |> getTwoWayValidateData

        test <@ d.Set (box p) m |> unbox = set (ValueSome p) @>
      }


    [<Fact>]
    let ``when final set receives null, original get receives ValueNone`` () =
      Property.check <| property {
        let! m = GenX.auto<int>

        let set (p: string voption) = p |> ValueOption.map (fun x -> x + x)
        let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> Result<_,_>)) |> getTwoWayValidateData

        test <@ d.Set null m |> unbox = set ValueNone @>
      }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! ok = GenX.auto<byte>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then Ok ok else Error err
        let d = Binding.twoWayOptValidate((fail: _ -> _ voption), (fail: _ -> int), validate) |> getTwoWayValidateData

        test <@ d.Validate x |> unbox = (validate x |> ValueOption.ofError) @>
      }



  module option_noSetModel_validateVoption =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.twoWayOptValidate((fail: _ -> _ option), (fail: _ -> int), (fail: _ -> _ voption))
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``when original get returns Some, final get returns the inner value`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get = string >> Some
        let d = Binding.twoWayOptValidate(get, (fail: _ -> int), (fail: _ -> _ voption)) |> getTwoWayValidateData

        test <@ d.Get x |> unbox = (get x).Value @>
      }


    [<Fact>]
    let ``when original get returns None, final get returns null`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get m = None
        let d = Binding.twoWayOptValidate(get, (fail: _ -> int), (fail: _ -> _ voption)) |> getTwoWayValidateData

        test <@ isNull (d.Get x) @>
      }


    [<Fact>]
    let ``when final set receives a non-null value, original get receives Some`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let set (p: string option) = p |> Option.map (fun x -> x + x)
        let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> _ voption)) |> getTwoWayValidateData

        test <@ d.Set (box p) m |> unbox = set (Some p) @>
      }


    [<Fact>]
    let ``when final set receives null, original get receives None`` () =
      Property.check <| property {
        let! m = GenX.auto<int>

        let set (p: string option) = p |> Option.map (fun x -> x + x)
        let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> _ voption)) |> getTwoWayValidateData

        test <@ d.Set null m |> unbox = set None @>
      }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then ValueSome err else ValueNone
        let d = Binding.twoWayOptValidate((fail: _ -> _ option), (fail: _ -> int), validate) |> getTwoWayValidateData

        test <@ d.Validate x |> unbox = validate x @>
      }



  module option_noSetModel_validateOption =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.twoWayOptValidate((fail: _ -> _ option), (fail: _ -> int), (fail: _ -> _ option))
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``when original get returns Some, final get returns the inner value`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get = string >> Some
        let d = Binding.twoWayOptValidate(get, (fail: _ -> int), (fail: _ -> _ option)) |> getTwoWayValidateData

        test <@ d.Get x |> unbox = (get x).Value @>
      }


    [<Fact>]
    let ``when original get returns None, final get returns null`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get m = None
        let d = Binding.twoWayOptValidate(get, (fail: _ -> int), (fail: _ -> _ option)) |> getTwoWayValidateData

        test <@ isNull (d.Get x) @>
      }


    [<Fact>]
    let ``when final set receives a non-null value, original get receives Some`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let set (p: string option) = p |> Option.map (fun x -> x + x)
        let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> _ option)) |> getTwoWayValidateData

        test <@ d.Set (box p) m |> unbox = set (Some p) @>
      }


    [<Fact>]
    let ``when final set receives null, original get receives None`` () =
      Property.check <| property {
        let! m = GenX.auto<int>

        let set (p: string option) = p |> Option.map (fun x -> x + x)
        let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> _ option)) |> getTwoWayValidateData

        test <@ d.Set null m |> unbox = set None @>
      }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then Some err else None
        let d = Binding.twoWayOptValidate((fail: _ -> _ option), (fail: _ -> int), validate) |> getTwoWayValidateData

        test <@ d.Validate x |> unbox = (validate x |> ValueOption.ofOption) @>
      }



  module option_noSetModel_validateResult =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.twoWayOptValidate((fail: _ -> _ option), (fail: _ -> int), (fail: _ -> Result<_,_>))
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``when original get returns Some, final get returns the inner value`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get = string >> Some
        let d = Binding.twoWayOptValidate(get, (fail: _ -> int), (fail: _ -> Result<_,_>)) |> getTwoWayValidateData

        test <@ d.Get x |> unbox = (get x).Value @>
      }


    [<Fact>]
    let ``when original get returns None, final get returns null`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let get m = None
        let d = Binding.twoWayOptValidate(get, (fail: _ -> int), (fail: _ -> Result<_,_>)) |> getTwoWayValidateData

        test <@ isNull (d.Get x) @>
      }


    [<Fact>]
    let ``when final set receives a non-null value, original get receives Some`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let set (p: string option) = p |> Option.map (fun x -> x + x)
        let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> Result<_,_>)) |> getTwoWayValidateData

        test <@ d.Set (box p) m |> unbox = set (Some p) @>
      }


    [<Fact>]
    let ``when final set receives null, original get receives None`` () =
      Property.check <| property {
        let! m = GenX.auto<int>

        let set (p: string option) = p |> Option.map (fun x -> x + x)
        let d = Binding.twoWayOptValidate(fail, set, (fail: _ -> Result<_,_>)) |> getTwoWayValidateData

        test <@ d.Set null m |> unbox = set None @>
      }


    [<Fact>]
    let ``final validate returns value from original validate`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! ok = GenX.auto<byte>
        let! err = GenX.auto<string>

        let validate x = if x < 0 then Ok ok else Error err
        let d = Binding.twoWayOptValidate((fail: _ -> _ option), (fail: _ -> int), validate) |> getTwoWayValidateData

        test <@ d.Validate x |> unbox = (validate x |> ValueOption.ofError) @>
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


    [<Fact>]
    let ``final exec returns value from original exec wrapped in ValueSome`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let exec = string
        let d = Binding.cmd(exec) |> getCmdData

        test <@ d.Exec x = (exec x |> ValueSome) @>
      }


    [<Fact>]
    let ``canExec always returns true`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let d = Binding.cmd(fail) |> getCmdData
        test <@ d.CanExec x = true @>
      }



  module noModel =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.cmd(obj())
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final exec returns original value wrapped in ValueSome`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! y = GenX.auto<byte>

        let d = Binding.cmd(y) |> getCmdData

        test <@ d.Exec x = ValueSome y @>
      }


    [<Fact>]
    let ``canExec always returns true`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let d = Binding.cmd(obj()) |> getCmdData
        test <@ d.CanExec x = true @>
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


    [<Fact>]
    let ``final exec returns value from original exec wrapped in ValueSome`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let exec = string
        let d = Binding.cmdIf(exec, fail) |> getCmdData

        test <@ d.Exec x = (exec x |> ValueSome) @>
      }


    [<Fact>]
    let ``final canExec returns value from original canExec`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let canExec x = x > 0
        let d = Binding.cmdIf(fail, canExec) |> getCmdData

        test <@ d.CanExec x = canExec x @>
      }


  module explicitCanExec_noModel =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.cmdIf(obj(), fail)
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final exec returns original value wrapped in ValueSome`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! y = GenX.auto<byte>

        let d = Binding.cmdIf(y, fail) |> getCmdData

        test <@ d.Exec x = ValueSome y @>
      }


    [<Fact>]
    let ``final canExec returns value from original canExec`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let canExec x = x > 0
        let d = Binding.cmdIf(obj(), canExec) |> getCmdData

        test <@ d.CanExec x = canExec x @>
      }



  module voption =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.cmdIf(fail: _ -> _ voption)
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final exec returns value from original exec`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let exec x = if x < 0 then ValueNone else ValueSome (string x)
        let d = Binding.cmdIf(exec) |> getCmdData

        test <@ d.Exec x = exec x @>
      }


    [<Fact>]
    let ``final canExec returns true if original exec returns ValueSome`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let exec x = ValueSome (string x)
        let d = Binding.cmdIf(exec) |> getCmdData

        test <@ d.CanExec x = true @>
      }


    [<Fact>]
    let ``final canExec returns false if original exec returns ValueNone`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let exec x = ValueNone
        let d = Binding.cmdIf(exec) |> getCmdData

        test <@ d.CanExec x = false @>
      }



  module option =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.cmdIf(fail: _ -> _ option)
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final exec returns value from original exec converted to ValueOption`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let exec x = if x < 0 then None else Some (string x)
        let d = Binding.cmdIf(exec) |> getCmdData

        test <@ d.Exec x = (exec x |> ValueOption.ofOption) @>
      }


    [<Fact>]
    let ``final canExec returns true if original exec returns Some`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let exec x = Some (string x)
        let d = Binding.cmdIf(exec) |> getCmdData

        test <@ d.CanExec x = true @>
      }


    [<Fact>]
    let ``final canExec returns false if original exec returns None`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let exec x = None
        let d = Binding.cmdIf(exec) |> getCmdData

        test <@ d.CanExec x = false @>
      }



  module result =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.cmdIf(fail: _ -> Result<_,_>)
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final exec returns Ok value from original exec converted to ValueOption`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let exec x = if x < 0 then Error (float x) else Ok (string x)
        let d = Binding.cmdIf(exec) |> getCmdData

        test <@ d.Exec x = (exec x |> ValueOption.ofOk) @>
      }


    [<Fact>]
    let ``final canExec returns true if original exec returns Ok`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let exec x = Ok (string x)
        let d = Binding.cmdIf(exec) |> getCmdData

        test <@ d.CanExec x = true @>
      }


    [<Fact>]
    let ``final canExec returns false if original exec returns Error`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let exec x = Error (float x)
        let d = Binding.cmdIf(exec) |> getCmdData

        test <@ d.CanExec x = false @>
      }



module cmdParam =


  module model =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.cmdParam(fail, fail2)
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final exec returns value from original exec wrapped in ValueSome`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let exec (p: obj) (m: int) = unbox p + string m
        let d = Binding.cmdParam(fail, exec) |> getCmdParamData

        test <@ d.Exec (box p) m = (exec p m |> ValueSome) @>
      }


    [<Fact>]
    let ``canExec always returns true`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>
        let d = Binding.cmdParam(fail, fail2) |> getCmdParamData
        test <@ d.CanExec (box p) m = true @>
      }


    [<Fact>]
    let ``autoRequery is false`` () =
      Property.check <| property {
        let d = Binding.cmdParam(fail, fail2) |> getCmdParamData
        test <@ d.AutoRequery = false @>
      }



  module noModel =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.cmdParam(fail, fail)
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final exec returns original value wrapped in ValueSome`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let exec p = string p
        let d = Binding.cmdParam(fail, exec) |> getCmdParamData

        test <@ d.Exec (box p) m = (exec p |> ValueSome) @>
      }


    [<Fact>]
    let ``canExec always returns true`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>
        let d = Binding.cmdParam(fail, fail) |> getCmdParamData
        test <@ d.CanExec (box p) m = true @>
      }


    [<Fact>]
    let ``autoRequery is false`` () =
      Property.check <| property {
        let d = Binding.cmdParam(fail, fail) |> getCmdParamData
        test <@ d.AutoRequery = false @>
      }



module cmdParamIf =


  module explicitCanExec_model =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.cmdParamIf(fail, fail, fail)
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final exec returns value from original exec wrapped in ValueSome`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let cast (p: obj) = unbox<string> p
        let exec p m = p + string m
        let d = Binding.cmdParamIf(cast, exec, fail) |> getCmdParamData

        test <@ d.Exec (box p) m = (exec p m |> ValueSome) @>
      }


    [<Fact>]
    let ``final canExec returns value from original canExec`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let cast (p: obj) = unbox<string> p
        let canExec (p: string) m = p.Length + m > 0
        let d = Binding.cmdParamIf(cast, fail, canExec) |> getCmdParamData

        test <@ d.CanExec (box p) m = canExec p m @>
      }


    [<Fact>]
    let ``final autoRequery defaults to false`` () =
      Property.check <| property {
        let d = Binding.cmdParamIf(fail, fail, fail) |> getCmdParamData
        test <@ d.AutoRequery = false @>
      }


    [<Fact>]
    let ``final autoRequery equals original uiBoundCmdParam`` () =
      Property.check <| property {
        let! uiBoundCmdParam = GenX.auto<bool>
        let d = Binding.cmdParamIf(fail, fail, fail, uiBoundCmdParam = uiBoundCmdParam, wrapDispatch = id) |> getCmdParamData
        test <@ d.AutoRequery = uiBoundCmdParam @>
      }


  module voption_model =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.cmdParamIf(fail, (fail2: _ -> _ -> _ voption))
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final exec returns value from original exec`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let cast (p: obj) = p :?> string
        let exec (p: string) m = p.Length + m |> ValueSome |> ValueOption.filter (fun x -> x > 0)
        let d = Binding.cmdParamIf(cast, exec) |> getCmdParamData

        test <@ d.Exec (box p) m = exec p m @>
      }


    [<Fact>]
    let ``final canExec returns true if original exec returns ValueSome`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let cast (p: obj) = p :?> string
        let exec (p: string) m = p.Length + m |> ValueSome
        let d = Binding.cmdParamIf(cast, exec) |> getCmdParamData

        test <@ d.CanExec (box p) m = true @>
      }


    [<Fact>]
    let ``final canExec returns false if original exec returns ValueNone`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let exec (p: obj) m = ValueNone
        let d = Binding.cmdParamIf(id, exec) |> getCmdParamData

        test <@ d.CanExec (box p) m = false @>
      }


    [<Fact>]
    let ``final autoRequery defaults to false`` () =
      Property.check <| property {
        let d = Binding.cmdParamIf(fail, (fail2: _ -> _ -> _ voption)) |> getCmdParamData
        test <@ d.AutoRequery = false @>
      }


    [<Fact>]
    let ``final autoRequery equals original uiBoundCmdParam`` () =
      Property.check <| property {
        let! uiBoundCmdParam = GenX.auto<bool>
        let d = Binding.cmdParamIf(fail, (fail2: _ -> _ -> _ voption), uiBoundCmdParam = uiBoundCmdParam) |> getCmdParamData
        test <@ d.AutoRequery = uiBoundCmdParam @>
      }



  module option_model =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.cmdParamIf(fail, (fail2: _ -> _ -> _ option))
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final exec returns value from original exec converted to ValueOption`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let cast (p: obj) = p :?> string
        let exec (p: string) m = p.Length + m |> Some |> Option.filter (fun x -> x > 0)
        let d = Binding.cmdParamIf(cast, exec) |> getCmdParamData

        test <@ d.Exec (box p) m = (exec p m |> ValueOption.ofOption) @>
      }


    [<Fact>]
    let ``final canExec returns true if original exec returns Some`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let cast (p: obj) = p :?> string
        let exec (p: string) m = p.Length + m |> Some
        let d = Binding.cmdParamIf(cast, exec) |> getCmdParamData

        test <@ d.CanExec (box p) m = true @>
      }


    [<Fact>]
    let ``final canExec returns false if original exec returns None`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let exec (p: obj) m = None
        let d = Binding.cmdParamIf(id, exec) |> getCmdParamData

        test <@ d.CanExec (box p) m = false @>
      }


    [<Fact>]
    let ``final autoRequery defaults to false`` () =
      Property.check <| property {
        let d = Binding.cmdParamIf(fail, (fail2: _ -> _ -> _ option)) |> getCmdParamData
        test <@ d.AutoRequery = false @>
      }


    [<Fact>]
    let ``final autoRequery equals original uiBoundCmdParam`` () =
      Property.check <| property {
        let! uiBoundCmdParam = GenX.auto<bool>
        let d = Binding.cmdParamIf(fail, (fail2: _ -> _ -> _ option), uiBoundCmdParam = uiBoundCmdParam) |> getCmdParamData
        test <@ d.AutoRequery = uiBoundCmdParam @>
      }



  module result_model =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.cmdParamIf(fail, (fail2: _ -> _ -> Result<_,_>))
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final exec returns Ok value from original exec converted to ValueOption`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let cast (p: obj) = p :?> string
        let exec (p: string) m =
          let x = p.Length + m
          if x > 0 then Ok x else Error (string x)
        let d = Binding.cmdParamIf(cast, exec) |> getCmdParamData

        test <@ d.Exec (box p) m = (exec p m |> ValueOption.ofOk) @>
      }


    [<Fact>]
    let ``final canExec returns true if original exec returns Ok`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let cast (p: obj) = p :?> string
        let exec (p: string) m = p.Length + m |> Ok
        let d = Binding.cmdParamIf(cast, exec) |> getCmdParamData

        test <@ d.CanExec (box p) m = true @>
      }


    [<Fact>]
    let ``final canExec returns false if original exec returns Error`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>
        let! err = GenX.auto<byte>

        let exec (p: obj) m = Error err
        let d = Binding.cmdParamIf(id, exec) |> getCmdParamData

        test <@ d.CanExec (box p) m = false @>
      }


    [<Fact>]
    let ``final autoRequery defaults to false`` () =
      Property.check <| property {
        let d = Binding.cmdParamIf(fail, (fail2: _ -> _ -> Result<_,_>)) |> getCmdParamData
        test <@ d.AutoRequery = false @>
      }


    [<Fact>]
    let ``final autoRequery equals original uiBoundCmdParam`` () =
      Property.check <| property {
        let! uiBoundCmdParam = GenX.auto<bool>
        let d = Binding.cmdParamIf(fail, (fail2: _ -> _ -> Result<_,_>), uiBoundCmdParam = uiBoundCmdParam) |> getCmdParamData
        test <@ d.AutoRequery = uiBoundCmdParam @>
      }



  module explicitCanExec_noModel =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.cmdParamIf(fail, (fail: obj -> obj), fail)
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final exec returns value from original exec wrapped in ValueSome`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let cast (p: obj) = unbox<string> p
        let exec (p: string) = p.Length
        let d = Binding.cmdParamIf(cast, exec, fail) |> getCmdParamData

        test <@ d.Exec (box p) m = (exec p |> ValueSome) @>
      }


    [<Fact>]
    let ``final canExec returns value from original canExec`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let cast (p: obj) = unbox<string> p
        let canExec (p: string) = p.Length + m > 0
        let d = Binding.cmdParamIf(cast, fail, canExec) |> getCmdParamData

        test <@ d.CanExec (box p) m = canExec p @>
      }


    [<Fact>]
    let ``final autoRequery defaults to false`` () =
      Property.check <| property {
        let d = Binding.cmdParamIf(fail, fail, fail) |> getCmdParamData
        test <@ d.AutoRequery = false @>
      }


    [<Fact>]
    let ``final autoRequery equals original uiBoundCmdParam`` () =
      Property.check <| property {
        let! uiBoundCmdParam = GenX.auto<bool>
        let d = Binding.cmdParamIf(fail, (fail: obj -> obj), fail, uiBoundCmdParam = uiBoundCmdParam) |> getCmdParamData
        test <@ d.AutoRequery = uiBoundCmdParam @>
      }


  module voption_noModel =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.cmdParamIf(fail, (fail: _ -> _ voption))
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final exec returns value from original exec`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let cast (p: obj) = p :?> string
        let exec (p: string) = p.Length |> ValueSome |> ValueOption.filter (fun x -> x > 0)
        let d = Binding.cmdParamIf(cast, exec) |> getCmdParamData

        test <@ d.Exec (box p) m = exec p @>
      }


    [<Fact>]
    let ``final canExec returns true if original exec returns ValueSome`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let cast (p: obj) = p :?> string
        let exec (p: string) = p.Length |> ValueSome
        let d = Binding.cmdParamIf(cast, exec) |> getCmdParamData

        test <@ d.CanExec (box p) m = true @>
      }


    [<Fact>]
    let ``final canExec returns false if original exec returns ValueNone`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let exec (p: obj) = ValueNone
        let d = Binding.cmdParamIf(id, exec) |> getCmdParamData

        test <@ d.CanExec (box p) m = false @>
      }


    [<Fact>]
    let ``final autoRequery defaults to false`` () =
      Property.check <| property {
        let d = Binding.cmdParamIf(fail, (fail: _ -> _ voption)) |> getCmdParamData
        test <@ d.AutoRequery = false @>
      }


    [<Fact>]
    let ``final autoRequery equals original uiBoundCmdParam`` () =
      Property.check <| property {
        let! uiBoundCmdParam = GenX.auto<bool>
        let d = Binding.cmdParamIf(fail, (fail: _ -> _ voption), uiBoundCmdParam = uiBoundCmdParam) |> getCmdParamData
        test <@ d.AutoRequery = uiBoundCmdParam @>
      }



  module option_noModel =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.cmdParamIf(fail, (fail: _ -> _ option))
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final exec returns value from original exec converted to ValueOption`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let cast (p: obj) = p :?> string
        let exec (p: string) = p.Length |> Some |> Option.filter (fun x -> x > 0)
        let d = Binding.cmdParamIf(cast, exec) |> getCmdParamData

        test <@ d.Exec (box p) m = (exec p |> ValueOption.ofOption) @>
      }


    [<Fact>]
    let ``final canExec returns true if original exec returns Some`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let cast (p: obj) = p :?> string
        let exec (p: string) = p.Length |> Some
        let d = Binding.cmdParamIf(cast, exec) |> getCmdParamData

        test <@ d.CanExec (box p) m = true @>
      }


    [<Fact>]
    let ``final canExec returns false if original exec returns None`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let exec (p: obj) = None
        let d = Binding.cmdParamIf(id, exec) |> getCmdParamData

        test <@ d.CanExec (box p) m = false @>
      }


    [<Fact>]
    let ``final autoRequery defaults to false`` () =
      Property.check <| property {
        let d = Binding.cmdParamIf(fail, (fail: _ -> _ option)) |> getCmdParamData
        test <@ d.AutoRequery = false @>
      }


    [<Fact>]
    let ``final autoRequery equals original uiBoundCmdParam`` () =
      Property.check <| property {
        let! uiBoundCmdParam = GenX.auto<bool>
        let d = Binding.cmdParamIf(fail, (fail: _ -> _ option), uiBoundCmdParam = uiBoundCmdParam) |> getCmdParamData
        test <@ d.AutoRequery = uiBoundCmdParam @>
      }



  module result_noModel =


    [<Fact>]
    let ``sets the correct binding name`` () =
      Property.check <| property {
        let! bindingName = GenX.auto<string>
        let binding = bindingName |> Binding.cmdParamIf(fail, (fail: _ -> Result<_,_>))
        test <@ binding.Name = bindingName @>
      }


    [<Fact>]
    let ``final exec returns Ok value from original exec converted to ValueOption`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let cast (p: obj) = p :?> string
        let exec (p: string) =
          let x = p.Length
          if x > 0 then Ok x else Error (string x)
        let d = Binding.cmdParamIf(cast, exec) |> getCmdParamData

        test <@ d.Exec (box p) m = (exec p |> ValueOption.ofOk) @>
      }


    [<Fact>]
    let ``final canExec returns true if original exec returns Ok`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>

        let cast (p: obj) = p :?> string
        let exec (p: string) = p.Length |> Ok
        let d = Binding.cmdParamIf(cast, exec) |> getCmdParamData

        test <@ d.CanExec (box p) m = true @>
      }


    [<Fact>]
    let ``final canExec returns false if original exec returns Error`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string>
        let! err = GenX.auto<byte>

        let exec (p: obj) = Error err
        let d = Binding.cmdParamIf(id, exec) |> getCmdParamData

        test <@ d.CanExec (box p) m = false @>
      }


    [<Fact>]
    let ``final autoRequery defaults to false`` () =
      Property.check <| property {
        let d = Binding.cmdParamIf(fail, (fail: _ -> Result<_,_>)) |> getCmdParamData
        test <@ d.AutoRequery = false @>
      }


    [<Fact>]
    let ``final autoRequery equals original uiBoundCmdParam`` () =
      Property.check <| property {
        let! uiBoundCmdParam = GenX.auto<bool>
        let d = Binding.cmdParamIf(fail, (fail: _ -> Result<_,_>), uiBoundCmdParam = uiBoundCmdParam) |> getCmdParamData
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
        let getSubModel = string
        let d = Binding.subModel(getSubModel, fail) |> getSubModelData
        test <@ d.GetModel x = ((x, getSubModel x) |> box |> ValueSome) @>
      }


    [<Fact>]
    let ``final toMsg simply unboxes`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let d = Binding.subModel(fail, fail) |> getSubModelData
        test <@ d.ToMsg (box x) = x @>
      }


    [<Fact>]
    let ``sticky is false`` () =
      Property.check <| property {
        let d = Binding.subModel(fail, fail) |> getSubModelData
        test <@ d.Sticky = false @>
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
        let getSubModel = string
        let d = Binding.subModel(getSubModel, fail, fail) |> getSubModelData
        test <@ d.GetModel x = ((x, getSubModel x) |> box |> ValueSome) @>
      }


    [<Fact>]
    let ``final toMsg returns value from original toMsg`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let toMsg = string
        let d = Binding.subModel(fail, toMsg, fail) |> getSubModelData

        test <@ d.ToMsg (box x) = toMsg x @>
      }


    [<Fact>]
    let ``sticky is false`` () =
      Property.check <| property {
        let d = Binding.subModel(fail, fail, fail) |> getSubModelData
        test <@ d.Sticky = false @>
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
        let getSubModel = string
        let toBindingModel (m: int, s: string) = m + s.Length
        let d = Binding.subModel(getSubModel, toBindingModel, fail, fail) |> getSubModelData
        test <@ d.GetModel x = ((x, getSubModel x) |> toBindingModel |> box |> ValueSome) @>
      }


    [<Fact>]
    let ``final toMsg returns value from original toMsg`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let toMsg = string
        let d = Binding.subModel(fail, fail, toMsg, fail) |> getSubModelData

        test <@ d.ToMsg (box x) = toMsg x @>
      }


    [<Fact>]
    let ``sticky is false`` () =
      Property.check <| property {
        let d = Binding.subModel(fail, fail, fail, fail) |> getSubModelData
        test <@ d.Sticky = false @>
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
        let getSubModel (m: int) : string voption = ValueNone
        let d = Binding.subModelOpt(getSubModel, fail) |> getSubModelData
        test <@ d.GetModel x = ValueNone @>
      }


    [<Fact>]
    let ``final toMsg simply unboxes`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let d = Binding.subModelOpt((fail: _ -> _ voption), fail) |> getSubModelData
        test <@ d.ToMsg (box x) = x @>
      }


    [<Fact>]
    let ``sticky defaults to false`` () =
      Property.check <| property {
        let d = Binding.subModelOpt((fail: _ -> _ voption), fail) |> getSubModelData
        test <@ d.Sticky = false @>
      }


    [<Fact>]
    let ``sticky follows input param`` () =
      Property.check <| property {
        let! sticky = Gen.bool
        let d = Binding.subModelOpt((fail: _ -> _ voption), fail, sticky = sticky) |> getSubModelData
        test <@ d.Sticky = sticky @>
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
        let getSubModel (m: int) : string option = None
        let d = Binding.subModelOpt(getSubModel, fail) |> getSubModelData
        test <@ d.GetModel x = ValueNone @>
      }


    [<Fact>]
    let ``final toMsg simply unboxes`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let d = Binding.subModelOpt((fail: _ -> _ option), fail) |> getSubModelData
        test <@ d.ToMsg (box x) = x @>
      }


    [<Fact>]
    let ``sticky defaults to false`` () =
      Property.check <| property {
        let d = Binding.subModelOpt((fail: _ -> _ option), fail) |> getSubModelData
        test <@ d.Sticky = false @>
      }


    [<Fact>]
    let ``sticky follows input param`` () =
      Property.check <| property {
        let! sticky = Gen.bool
        let d = Binding.subModelOpt((fail: _ -> _ option), fail, sticky = sticky) |> getSubModelData
        test <@ d.Sticky = sticky @>
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
        let getSubModel (m: int) : string voption = ValueNone
        let d = Binding.subModelOpt(getSubModel, fail, fail) |> getSubModelData
        test <@ d.GetModel x = ValueNone @>
      }


    [<Fact>]
    let ``final toMsg returns value from original toMsg`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let toMsg = string
        let d = Binding.subModelOpt((fail: _ -> _ voption), toMsg, fail) |> getSubModelData

        test <@ d.ToMsg (box x) = toMsg x @>
      }


    [<Fact>]
    let ``sticky defaults to false`` () =
      Property.check <| property {
        let d = Binding.subModelOpt((fail: _ -> _ voption), fail, fail) |> getSubModelData
        test <@ d.Sticky = false @>
      }


    [<Fact>]
    let ``sticky follows input param`` () =
      Property.check <| property {
        let! sticky = Gen.bool
        let d = Binding.subModelOpt((fail: _ -> _ voption), fail, fail, sticky = sticky) |> getSubModelData
        test <@ d.Sticky = sticky @>
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
        let getSubModel (m: int) : string option = None
        let d = Binding.subModelOpt(getSubModel, fail, fail) |> getSubModelData
        test <@ d.GetModel x = ValueNone @>
      }


    [<Fact>]
    let ``final toMsg returns value from original toMsg`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let toMsg = string
        let d = Binding.subModelOpt((fail: _ -> _ option), toMsg, fail) |> getSubModelData

        test <@ d.ToMsg (box x) = toMsg x @>
      }


    [<Fact>]
    let ``sticky defaults to false`` () =
      Property.check <| property {
        let d = Binding.subModelOpt((fail: _ -> _ option), fail, fail) |> getSubModelData
        test <@ d.Sticky = false @>
      }


    [<Fact>]
    let ``sticky follows input param`` () =
      Property.check <| property {
        let! sticky = Gen.bool
        let d = Binding.subModelOpt((fail: _ -> _ option), fail, fail, sticky = sticky) |> getSubModelData
        test <@ d.Sticky = sticky @>
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
        let getSubModel (m: int) : string voption = ValueNone
        let d = Binding.subModelOpt(getSubModel, fail, fail, fail) |> getSubModelData
        test <@ d.GetModel x = ValueNone @>
      }


    [<Fact>]
    let ``final toMsg returns value from original toMsg`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let toMsg = string
        let d = Binding.subModelOpt((fail: _ -> _ voption), fail, toMsg, fail) |> getSubModelData

        test <@ d.ToMsg (box x) = toMsg x @>
      }


    [<Fact>]
    let ``sticky defaults to false`` () =
      Property.check <| property {
        let d = Binding.subModelOpt((fail: _ -> _ voption), fail, fail, fail) |> getSubModelData
        test <@ d.Sticky = false @>
      }


    [<Fact>]
    let ``sticky follows input param`` () =
      Property.check <| property {
        let! sticky = Gen.bool
        let d = Binding.subModelOpt((fail: _ -> _ voption), fail, fail, fail, sticky = sticky) |> getSubModelData
        test <@ d.Sticky = sticky @>
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
        let getSubModel (m: int) : string option = None
        let d = Binding.subModelOpt(getSubModel, fail, fail, fail) |> getSubModelData
        test <@ d.GetModel x = ValueNone @>
      }


    [<Fact>]
    let ``final toMsg returns value from original toMsg`` () =
      Property.check <| property {
        let! x = GenX.auto<int>

        let toMsg = string
        let d = Binding.subModelOpt((fail: _ -> _ option), fail, toMsg, fail) |> getSubModelData

        test <@ d.ToMsg (box x) = toMsg x @>
      }


    [<Fact>]
    let ``sticky defaults to false`` () =
      Property.check <| property {
        let d = Binding.subModelOpt((fail: _ -> _ option), fail, fail, fail) |> getSubModelData
        test <@ d.Sticky = false @>
      }


    [<Fact>]
    let ``sticky follows input param`` () =
      Property.check <| property {
        let! sticky = Gen.bool
        let d = Binding.subModelOpt((fail: _ -> _ option), fail, fail, fail, sticky = sticky) |> getSubModelData
        test <@ d.Sticky = sticky @>
      }



module subModelSeq =


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
        let d = Binding.subModelSeq(getSubModels, fail, fail) |> getSubModelSeqData
        test <@ d.GetModels m |> Seq.map unbox |> Seq.toList = (m |> getSubModels |> List.map (fun s -> m, s)) @>
      }


    [<Fact>]
    let ``final getId returns the ID of each element in final getModels`` () =
      Property.check <| property {
        let! m = GenX.auto<string>
        let getSubModels : string -> char list = Seq.toList
        let getId : char -> string = string
        let d = Binding.subModelSeq(getSubModels, getId, fail) |> getSubModelSeqData
        test <@ d.GetModels m |> Seq.map d.GetId |> Seq.map unbox |> Seq.toList = (m |> getSubModels |> List.map getId) @>
      }


    [<Fact>]
    let ``final toMsg extracts and unboxes the second tuple element`` () =
      Property.check <| property {
        let! x = GenX.auto<int>
        let! y = GenX.auto<string>
        let d = Binding.subModelSeq(fail, fail, fail) |> getSubModelSeqData
        test <@ d.ToMsg (box x, box y) |> unbox = y @>
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
        let d = Binding.subModelSeq(getSubModels, fail, fail, fail) |> getSubModelSeqData
        test <@ d.GetModels m |> Seq.map unbox |> Seq.toList = (m |> getSubModels |> List.map (fun s -> m, s)) @>
      }


    [<Fact>]
    let ``final getId returns the ID of each element in final getModels`` () =
      Property.check <| property {
        let! m = GenX.auto<string>
        let getSubModels : string -> char list = Seq.toList
        let getId : char -> string = string
        let d = Binding.subModelSeq(getSubModels, getId, fail, fail) |> getSubModelSeqData
        test <@ d.GetModels m |> Seq.map d.GetId |> Seq.map unbox |> Seq.toList = (m |> getSubModels |> List.map getId) @>
      }


    [<Fact>]
    let ``final toMsg returns the value of original toMsg`` () =
      Property.check <| property {
        let! id = GenX.auto<int>
        let! msg = GenX.auto<string>
        let toMsg (id: int, msg: string) = msg.Length + id
        let d = Binding.subModelSeq(fail, fail, toMsg, fail) |> getSubModelSeqData
        test <@ d.ToMsg (box id, box msg) |> unbox = toMsg (id, msg) @>
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
          let d = Binding.subModelSeq(getSubModels, toBindingModel, fail, fail, fail) |> getSubModelSeqData
          test <@ d.GetModels m |> Seq.map unbox |> Seq.toList = (m |> getSubModels |> List.map (fun s -> toBindingModel (m, s))) @>
        }


      [<Fact>]
      let ``final getId returns the ID of each element in final getModels`` () =
        Property.check <| property {
          let! m = GenX.auto<string>
          let getSubModels : string -> char list = Seq.toList
          let toBindingModel (m: string, c: char) = (m + string c).Length
          let getId i = i * 2
          let d = Binding.subModelSeq(getSubModels, toBindingModel, getId, fail, fail) |> getSubModelSeqData
          test <@ d.GetModels m |> Seq.map d.GetId |> Seq.map unbox |> Seq.toList = (m |> getSubModels |> List.map (fun s -> toBindingModel (m, s)) |> List.map getId) @>
        }


      [<Fact>]
      let ``final toMsg returns the value of original toMsg`` () =
        Property.check <| property {
          let! id = GenX.auto<int>
          let! msg = GenX.auto<string>
          let toMsg (id: int, msg: string) = msg.Length + id
          let d = Binding.subModelSeq(fail, fail, fail, toMsg, fail) |> getSubModelSeqData
          test <@ d.ToMsg (box id, box msg) |> unbox = toMsg (id, msg) @>
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
        let get x = if useNone then ValueNone else x |> string |> ValueSome
        let d = Binding.subModelSelectedItem("", get, fail2) |> getSubModelSelectedItemData
        test <@ d.Get (box x) |> ValueOption.map unbox = get x @>
      }


    [<Fact>]
    let ``final set returns value from original set`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string voption>
        let set (p: string voption) m = p |> ValueOption.map (fun p -> p.Length + m |> string)
        let d = Binding.subModelSelectedItem("", (fail: _ -> _ voption), set) |> getSubModelSelectedItemData
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
        let get x = if useNone then None else x |> string |> Some
        let d = Binding.subModelSelectedItem("", get, fail2) |> getSubModelSelectedItemData
        test <@ d.Get (box x) |> ValueOption.map unbox = (get x |> ValueOption.ofOption) @>
      }


    [<Fact>]
    let ``final set returns value from original set`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string option>
        let set (p: string option) m = p |> Option.map (fun p -> p.Length + m |> string)
        let d = Binding.subModelSelectedItem("", (fail: _ -> _ option), set) |> getSubModelSelectedItemData
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
          let get x = if useNone then ValueNone else x |> string |> ValueSome
          let d = Binding.subModelSelectedItem("", get, (fail: _ -> obj)) |> getSubModelSelectedItemData
          test <@ d.Get (box x) |> ValueOption.map unbox = get x @>
        }


      [<Fact>]
      let ``final set returns value from original set`` () =
        Property.check <| property {
          let! m = GenX.auto<int>
          let! p = GenX.auto<string voption>
          let set (p: string voption) = p |> ValueOption.map (fun p -> p.Length |> string)
          let d = Binding.subModelSelectedItem("", (fail: _ -> _ voption), set) |> getSubModelSelectedItemData
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
        let get x = if useNone then None else x |> string |> Some
        let d = Binding.subModelSelectedItem("", get, (fail: _ -> obj)) |> getSubModelSelectedItemData
        test <@ d.Get (box x) |> ValueOption.map unbox = (get x |> ValueOption.ofOption) @>
      }


    [<Fact>]
    let ``final set returns value from original set`` () =
      Property.check <| property {
        let! m = GenX.auto<int>
        let! p = GenX.auto<string option>
        let set (p: string option) = p |> Option.map (fun p -> p.Length |> string)
        let d = Binding.subModelSelectedItem("", (fail: _ -> _ option), set) |> getSubModelSelectedItemData
        test <@ d.Set (p |> Option.map box |> ValueOption.ofOption) m = set p @>
      }



module sorting =

  [<Fact>]
    let ``SubModelSelectedItemData sorted last`` () =
      Property.check <| property {
        let! s = GenX.auto<string>
        let! b = GenX.auto<bool>
        let! vo = GenX.auto<obj voption>
        let data =
          [ SubModelSelectedItemData { Get = fail; Set = fail2; SubModelSeqBindingName = s; WrapDispatch = fail }
            OneWayData { Get = fail }
            OneWayLazyData { Get = fail; Map = fail; Equals = fail2 }
            OneWaySeqLazyData { Get = fail; Map = fail; Equals = fail2; GetId = fail; ItemEquals = fail2 }
            TwoWayData { Get = fail; Set = fail2; WrapDispatch = fail }
            TwoWayValidateData { Get = fail; Set = fail2; Validate = fail; WrapDispatch = fail }
            CmdData { Exec = fail; CanExec = fail; WrapDispatch = fail }
            CmdParamData { Cast = fail; Exec = fail2; CanExec = fail2; AutoRequery = b; WrapDispatch = fail }
            SubModelData { GetModel = fail; GetBindings = fail; ToMsg = fail; Sticky = b }
            SubModelWinData { GetState = fail; GetBindings = fail; ToMsg = fail; GetWindow = fail2; IsModal = b; OnCloseRequested = vo }
            SubModelSeqData { GetModels = fail; GetId = fail; GetBindings = fail; ToMsg = fail }
            SubModelSelectedItemData { Get = fail; Set = fail2; SubModelSeqBindingName = s; WrapDispatch = fail }
          ]
        let sorted = data |> List.sortWith BindingData.subModelSelectedItemDataLast
        match sorted with
        | [_; _; _; _; _; _; _; _; _; _; SubModelSelectedItemData _; SubModelSelectedItemData _] -> ()
        | _ -> failwith "SubModelSelectedItemData was not sorted last"
      }
