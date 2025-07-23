module Elmish.WPF.Samples.Multiselect.Program

open System
open Serilog
open Serilog.Extensions.Logging
open Elmish.WPF

type Entity =
    { Id: int
      Name: string
      IsSelected: bool }

type Model = { Entities: Entity list }

let init () =
    { Entities =
        [ 0..10 ]
        |> List.map (fun i ->
            { Id = i
              Name = sprintf "Entity %i" i
              IsSelected = i < 5 }) }

type Msg =
    | SetIsSelected of int * bool
    | SelectRandom
    | DeselectAll

let rec update msg m =
    match msg with
    | SetIsSelected(entityId, isSelected) ->
        { m with
            Entities =
                m.Entities
                |> List.map (fun e ->
                    if e.Id = entityId then
                        { e with IsSelected = isSelected }
                    else
                        e) }
    | SelectRandom ->
        if m.Entities.Length > 0 then
            let id = m.Entities.Item(Random().Next(m.Entities.Length)).Id
            update (SetIsSelected(id, true)) m
        else
            m
    | DeselectAll ->
        { m with
            Entities = m.Entities |> List.map (fun e -> { e with IsSelected = false }) }

[<AllowNullLiteral>]
type EntityViewModel(args) =
    inherit ViewModelBase<Entity, int * bool>(args)

    let isSelectedBinding =
        Binding.TwoWayT.id
        >> Binding.addLazy (=)
        >> Binding.mapModel (fun e -> e.IsSelected)
        >> Binding.mapMsgWithModel (fun isSelected e -> (e.Id, isSelected))

    member _.Name =
        base.Get () (Binding.OneWayT.id >> Binding.addLazy (=) >> Binding.mapModel (fun e -> e.Name))

    member this.IsSelected
        with get () = base.Get () isSelectedBinding
        and set (value) = base.Set (value) isSelectedBinding

    member _.SelectedLabel =
        base.Get
            ()
            (Binding.OneWayT.id
             >> Binding.addLazy (=)
             >> Binding.mapModel (fun e -> if e.IsSelected then " - SELECTED" else ""))

[<AllowNullLiteral>]
type MainViewModel(args) =
    inherit ViewModelBase<Model, Msg>(args)

    let createEntityVm (args: ViewModelArgs<Entity, int * bool>) = EntityViewModel(args)

    member _.SelectRandom = base.Get () (Binding.CmdT.setAlways SelectRandom)

    member _.Deselect = base.Get () (Binding.CmdT.setAlways DeselectAll)

    member _.Entities =
        base.Get
            ()
            (Binding.SubModelSeqKeyedT.id createEntityVm (fun e -> e.Id)
             >> Binding.mapModel (fun m -> m.Entities)
             >> Binding.mapMsg (fun (_, (entityId, isSelected)) -> SetIsSelected(entityId, isSelected)))

let main window =
    let logger =
        LoggerConfiguration()
            .MinimumLevel.Override("Elmish.WPF.Update", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Bindings", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Performance", Events.LogEventLevel.Verbose)
            .WriteTo.Console()
            .CreateLogger()

    let createVm args = MainViewModel(args)

    WpfProgram.mkSimpleT init update createVm
    |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))
    |> WpfProgram.startElmishLoop window