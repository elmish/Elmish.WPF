module Elmish.WPF.Samples.Navigation.Program

open System
open Elmish
open Elmish.WPF
open Serilog
open Serilog.Extensions.Logging

[<RequireQualifiedAccess>]
module Dashboard =
    type Model = { Title: string }

    module Model =
        let empty = { Title = "Dashboard" }

    type Message = | Idle

    let init () = Model.empty, Cmd.none

    let update message model = model, Cmd.none

    let bindings () : Binding<Model, Message> list =
        [ "Title" |> Binding.oneWay (fun (m: Model) -> m.Title) ]

    let designVm = ViewModel.designInstance Model.empty (bindings ())

[<RequireQualifiedAccess>]
module Customer =
    type Model =
        { Title: string
          CurrentTime: DateTime option }

    module Model =
        let empty = { Title = "Kunden"; CurrentTime = None }

    type Message = | GetTime

    let init () = Model.empty, Cmd.none

    let update message model =
        match message with
        | GetTime ->
            { model with
                CurrentTime = Some(DateTime.Now) },
            Cmd.none

    let bindings () : Binding<Model, Message> list =
        [ "Title" |> Binding.oneWay (fun (m: Model) -> m.Title)
          "CurrentTime" |> Binding.oneWayOpt (fun (m: Model) -> m.CurrentTime)
          "GetTime" |> Binding.cmd GetTime ]

    let designVm = ViewModel.designInstance Model.empty (bindings ())

[<RequireQualifiedAccess>]
module Main =
    type Uri =
        | Dashboard = 0
        | Customer = 1

    type Page =
        | Dashboard of Dashboard.Model
        | Customer of Customer.Model

    type Model = { Title: string; ActivePage: Page }

    module Model =
        let empty =
            { Title = "App Navigation"
              ActivePage = Dashboard(fst (Dashboard.init ())) }

    type Message =
        | Navigate of obj
        | DashboardMessage of Dashboard.Message
        | CustomerMessage of Customer.Message

    let init () = Model.empty, Cmd.none

    let update message model =
        match message with
        | CustomerMessage msg ->
            match model.ActivePage with
            | Customer custModel ->
                let updatedCustModel, cmd = Customer.update msg custModel

                { model with
                    ActivePage = Customer updatedCustModel },
                Cmd.map CustomerMessage cmd
            | _ -> model, Cmd.none
        | DashboardMessage msg ->
            match model.ActivePage with
            | Dashboard dashModel ->
                let updatedDashModel, cmd = Dashboard.update msg dashModel

                { model with
                    ActivePage = Dashboard updatedDashModel },
                Cmd.map DashboardMessage cmd
            | _ -> model, Cmd.none
        | Navigate p ->
            match (p :?> Uri) with
            | Uri.Dashboard ->
                { model with
                    ActivePage = Dashboard(fst (Dashboard.init ())) },
                Cmd.none
            | Uri.Customer ->
                { model with
                    ActivePage = Customer(fst (Customer.init ())) },
                Cmd.none
            | _ -> System.ArgumentOutOfRangeException() |> raise

    let bindings () : Binding<Model, Message> list =
        [ "Title" |> Binding.oneWay (fun (m: Model) -> m.Title)
          "Navigate" |> Binding.cmdParam (fun param _ -> Navigate param)

          "Dashboard"
          |> Binding.SubModel.required Dashboard.bindings
          |> Binding.mapModel (fun (m: Model) ->
              match m.ActivePage with
              | Dashboard model -> model
              | _ -> fst (Dashboard.init ()))
          |> Binding.mapMsg DashboardMessage

          "Customer"
          |> Binding.SubModel.required Customer.bindings
          |> Binding.mapModel (fun (m: Model) ->
              match m.ActivePage with
              | Customer model -> model
              | _ -> fst (Customer.init ()))
          |> Binding.mapMsg CustomerMessage

          "ActivePageName"
          |> Binding.oneWay (fun (m: Model) ->
              match m.ActivePage with
              | Dashboard _ -> "Dashboard"
              | Customer _ -> "Customer") ]

    let designVm = ViewModel.designInstance Model.empty (bindings ())

let main window =
    let logger =
        LoggerConfiguration()
            .MinimumLevel.Override("Elmish.WPF.Update", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Bindings", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Performance", Events.LogEventLevel.Verbose)
            .WriteTo.Console()
            .CreateLogger()

    WpfProgram.mkProgram Main.init Main.update Main.bindings
    |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))
    |> WpfProgram.startElmishLoop window