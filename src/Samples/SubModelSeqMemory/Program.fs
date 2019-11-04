module Elmish.WPF.Samples.SubModelSeqMemory.Program

open System
open Elmish
open Elmish.WPF

let rand = Random()

[<AutoOpen>]
module Domain =

  type SubModelId = Guid

  type SubModel =
    { Id: SubModelId
      Num: float
      Data: float array }
    static member create () =
      { Id = Guid.NewGuid ()
        Num = 0.0
        Data = [| 0.0 .. 50000000.0 - 1.0 |] }
        
  type SubMsg =
    | Regen

module App =

  type Model =
    { SubModels: SubModel list
      CmdSubModels: SubModel list }

  let init () =
    { SubModels = []
      CmdSubModels = [] }

  type Msg =
    //| SubModelMsg of SubModelId
    | AddSubModel
    | AddCmdSubModel
    | RemoveFirstSubModel
    | RemoveFirstCmdSubModel
    | CmdSubModelMsg of SubModelId * SubMsg

  let subUpdate msg model =
    match msg with
    | Regen -> { model with Num = rand.NextDouble() } 

  let update msg model =
    match msg with
    | AddSubModel -> { model with SubModels = SubModel.create() :: model.SubModels }
    | AddCmdSubModel -> { model with CmdSubModels = SubModel.create() :: model.CmdSubModels }
    | RemoveFirstSubModel -> { model with SubModels = if model.SubModels |> List.isEmpty |> not then model.SubModels |> List.tail else [] }
    | RemoveFirstCmdSubModel -> { model with CmdSubModels = if model.CmdSubModels |> List.isEmpty |> not then model.CmdSubModels |> List.tail else [] }
    | CmdSubModelMsg (id, msg) ->
      let updateSubModel sm' = List.map (fun sm -> if sm'.Id = sm.Id then sm' else sm)
      model.CmdSubModels
      |> List.tryFind (fun sm -> sm.Id = id)
      |> Option.map (fun sm ->
        let sm' = subUpdate msg sm
        { model with CmdSubModels = model.CmdSubModels |> updateSubModel sm' })
      |> Option.defaultValue model
      


module Bindings =

  open App

  let subModelBindings () : Binding<SubModel, SubMsg> list = [
    "Id" |> Binding.oneWay (fun sm -> sm.Id)
    "Num" |> Binding.oneWay (fun sm -> sm.Num)
  ]

  let cmdSubModelBindings () : Binding<SubModel, SubMsg> list = [
    "Id" |> Binding.oneWay (fun sm -> sm.Id)
    "Num" |> Binding.oneWay (fun sm -> sm.Num)

    "Regen" |> Binding.cmd (fun sm -> Regen)
    //"Remove" |> Binding.cmd (fun sm -> Remove)

  ]


  let rootBindings () : Binding<Model, Msg> list = [
    "SubModels" |> Binding.subModelSeq ((fun m -> m.SubModels), snd, (fun sm -> sm.Id), CmdSubModelMsg, subModelBindings)

    "CmdSubModels" |> Binding.subModelSeq ((fun m -> m.CmdSubModels), snd, (fun sm -> sm.Id), CmdSubModelMsg, cmdSubModelBindings)

    "AddSubModel" |> Binding.cmd (fun _ -> AddSubModel)

    "AddCmdSubModel" |> Binding.cmd (fun _ -> AddCmdSubModel)

    "RemoveFirst" |> Binding.cmd (fun _ -> RemoveFirstSubModel)

    "RemoveFirstCmd" |> Binding.cmd (fun _ -> RemoveFirstCmdSubModel)
  ]


[<EntryPoint; STAThread>]
let main _ =
  Program.mkSimpleWpf App.init App.update Bindings.rootBindings
  |> Program.withConsoleTrace
  |> Program.runWindowWithConfig
    { ElmConfig.Default with LogConsole = true; Measure = true }
    (MainWindow())
