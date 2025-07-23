namespace Navigation.Core


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

type DashboardViewModel(args) =
    inherit ViewModelBase<Dashboard.Model, Dashboard.Message>(args)

    member _.Title = base.Get () (Binding.OneWayT.id >> Binding.mapModel _.Title)

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

type CustomerViewModel(args) =
    inherit ViewModelBase<Customer.Model, Customer.Message>(args)

    member _.Title = base.Get () (Binding.OneWayT.id >> Binding.mapModel _.Title)

    member _.CurrentTime =
        base.Get () (Binding.OneWayT.opt >> Binding.mapModel _.CurrentTime)

    member _.GetTime = base.Get () (Binding.CmdT.setAlways Customer.GetTime)

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

type MainViewModel(args) =
    inherit ViewModelBase<Main.Model, Main.Message>(args)

    member this.Title = base.Get () (Binding.OneWayT.id >> Binding.mapModel _.Title)

    member this.Dashboard =
        base.Get
            ()
            (Binding.SubModelT.req DashboardViewModel
             >> Binding.mapModel (fun (m: Main.Model) ->
                 match m.ActivePage with
                 | Main.Dashboard mdl -> mdl
                 | _ -> fst (Dashboard.init ()))
             >> Binding.mapMsg Main.DashboardMessage
             >> Binding.boxT)

    member this.Customer =
        base.Get
            ()
            (Binding.SubModelT.req CustomerViewModel
             >> Binding.mapModel (fun (m: Main.Model) ->
                 match m.ActivePage with
                 | Main.Customer mdl -> mdl
                 | _ -> fst (Customer.init ()))
             >> Binding.mapMsg Main.CustomerMessage
             >> Binding.boxT)

    // Map of page types to their ViewModel properties
    member this.GetViewModelForPage(page: Main.Page) =
        match page with
        | Main.Dashboard _ -> this.Dashboard
        | Main.Customer _ -> this.Customer

    member this.Content =
        base.Get
            ()
            (Binding.OneWayT.id
             >> Binding.mapModel (fun (m: Main.Model) -> this.GetViewModelForPage(m.ActivePage)))

    member _.Navigate =
        base.Get () (Binding.CmdT.id true (fun param model -> true) >> Binding.mapMsg Main.Navigate)

[<RequireQualifiedAccess>]
module Program =
    let run window =
        let logger =
            LoggerConfiguration()
                .MinimumLevel.Override("Elmish.WPF.Update", Events.LogEventLevel.Verbose)
                .MinimumLevel.Override("Elmish.WPF.Bindings", Events.LogEventLevel.Verbose)
                .MinimumLevel.Override("Elmish.WPF.Performance", Events.LogEventLevel.Verbose)
                .WriteTo.Console()
                .CreateLogger()

        WpfProgram.mkProgramT Main.init Main.update MainViewModel
        |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))
        |> WpfProgram.startElmishLoop window