namespace Program

open System
open Elmish
open Elmish.WPF
open Serilog
open Serilog.Extensions.Logging
open System.Windows.Media


[<AutoOpen>]
module Helpers =

    //• logging
    let createDashedLine () = String.replicate 69 "-"
    let logDashedLine = createDashedLine ()


    let generateName (prefix: string) =
        let randomNumber () = Random().Next(1000, 10000).ToString()
        prefix + randomNumber ()



module TextBox =

    type Model = { Id: Guid; Text: string }

    let create () =
        { Id = Guid.NewGuid()
          Text = generateName "TextBox_" }

    type Msg = | Dummy

    let init () = create ()

    let update msg m =
        match msg with
        | Dummy -> m


module CheckBox =

    type Model = { Id: Guid; Label: string }

    let create () =
        { Id = Guid.NewGuid()
          Label = generateName "CheckBox_" }

    type Msg = | Dummy

    let init () = create ()

    let update msg m =
        match msg with
        | Dummy -> m


module ComboBox =

    type Model =
        { Id: Guid
          Header: string
          Items: string list
          SelectedItem: string option }

    let create () =
        { Id = Guid.NewGuid()
          Header = generateName "ComboBox_"
          Items = [ "Option1"; "Option2"; "Option3" ]
          SelectedItem = None }

    type Msg = SelectItem of string option

    let init () = create ()

    let update msg m =
        match msg with
        | SelectItem item -> { m with SelectedItem = item }


//# Form Designer
module App =

    type FormComponent =
        | TextBox of TextBox.Model
        | CheckBox of CheckBox.Model
        | ComboBox of ComboBox.Model

    type Model =
        { Components: FormComponent list
          PreviousSelectedFormComponent: Guid option
          SelectedFormComponent: Guid option
          Log: string list }

    module ModelM =
        module Components =
            let get m = m.Components

    type Msg =
        | SelectFormComponent of Guid option
        | AddTextBox
        | AddCheckBox
        | AddComboBox
        | RemoveComponent of Guid
        | ClearLog
        //• SubModels
        | TextBox_Msg of Guid * TextBox.Msg
        | CheckBox_Msg of Guid * CheckBox.Msg
        | ComboBox_Msg of Guid * ComboBox.Msg

    let componentsMock =
        [ TextBox(TextBox.create ())
          CheckBox(CheckBox.create ())
          TextBox(TextBox.create ())
          CheckBox(CheckBox.create ())
          ComboBox(ComboBox.create ()) ]


    let getId (fc: FormComponent) =
        match fc with
        | TextBox tb -> tb.Id
        | CheckBox cb -> cb.Id
        | ComboBox cb -> cb.Id

    let init () =
        { Components = componentsMock
          SelectedFormComponent = Some(getId (List.item 3 componentsMock))
          PreviousSelectedFormComponent = None
          Log = [] },
        Cmd.none


    [<AutoOpen>]
    module private UpdateHelpers =
        //🞍 common
        let insertAt index item list =
            let before, after = List.splitAt index list
            before @ [ item ] @ after


        let getComponentName fc =
            match fc with
            | TextBox tb -> tb.Text
            | CheckBox cb -> cb.Label
            | ComboBox cb -> cb.Header


        let selectFormComponent id m =
            let logEntry =
                match id with
                | Some selectedId ->
                    match m.Components
                          |> List.tryFind (fun c -> getId c = selectedId)
                        with
                    | Some c ->
                        let name = getComponentName c
                        sprintf "Selected: %s" name
                    | None -> "Selected: Unknown component"
                | None -> "Deselected"

            { m with
                SelectedFormComponent = id
                Log = logEntry :: logDashedLine :: m.Log }


        //🞍 add/remove
        let addTextBox m =
            let newTextBox = TextBox.create ()

            let updatedTextBox = { newTextBox with Text = "###New### --- " + newTextBox.Text }

            let newComponent = TextBox updatedTextBox

            let components =
                match m.SelectedFormComponent with
                | Some selectedId ->
                    match m.Components
                          |> List.tryFindIndex (fun c -> getId c = selectedId)
                        with
                    | Some index -> insertAt (index + 1) newComponent m.Components
                    | None -> m.Components @ [ newComponent ]
                | None -> m.Components @ [ newComponent ]

            let log =
                ("Added: " + updatedTextBox.Text)
                :: ("Selected: " + updatedTextBox.Text)
                   :: logDashedLine :: m.Log

            { m with
                Components = components
                SelectedFormComponent = Some(getId newComponent)
                Log = log }


        let addCheckBox m =
            let newCheckBox = CheckBox.create ()

            let updatedCheckBox = { newCheckBox with Label = "###New### --- " + newCheckBox.Label }

            let newComponent = CheckBox updatedCheckBox

            let components =
                match m.SelectedFormComponent with
                | Some selectedId ->
                    match m.Components
                          |> List.tryFindIndex (fun c -> getId c = selectedId)
                        with
                    | Some index -> insertAt (index + 1) newComponent m.Components
                    | None -> m.Components @ [ newComponent ]
                | None -> m.Components @ [ newComponent ]

            let log =
                ("Added: " + updatedCheckBox.Label)
                :: ("Selected: " + updatedCheckBox.Label)
                   :: logDashedLine :: m.Log

            { m with
                Components = components
                SelectedFormComponent = Some(getId newComponent)
                Log = log }

                 
        let addComboBox m =
            let newComboBox = ComboBox.create ()

            let updateComboBox = { newComboBox with Header = "###New### --- " + newComboBox.Header }

            let newComponent = ComboBox newComboBox

            let components =
                match m.SelectedFormComponent with
                | Some selectedId ->
                    match m.Components
                          |> List.tryFindIndex (fun c -> getId c = selectedId)
                        with
                    | Some index -> insertAt (index + 1) newComponent m.Components
                    | None -> m.Components @ [ newComponent ]
                | None -> m.Components @ [ newComponent ]

            let log =
                ("Added: " + updateComboBox.Header)
                :: ("Selected: " + updateComboBox.Header)
                   :: logDashedLine :: m.Log

            { m with
                Components = components
                SelectedFormComponent = Some(getId newComponent)
                Log = log }


        let removeComponent id m : Model * Guid option * string list =
            let componentOpt =
                m.Components
                |> List.tryFind (fun c -> getId c = id)

            let removedComponentName =
                match componentOpt with
                | Some c -> getComponentName c
                | None -> "Unknown Component"

            let idxOpt =
                m.Components
                |> List.tryFindIndex (fun c -> getId c = id)

            let components = List.filter (fun c -> getId c <> id) m.Components

            let newSelected =
                match idxOpt with
                | Some idx when idx > 0 -> Some(getId (List.item (idx - 1) m.Components))
                | Some idx when components.Length > 0 -> Some(getId (List.item 0 components))
                | _ -> None

            let logs = [ "Removed: " + removedComponentName ]

            let m' = { m with Components = components }

            m', newSelected, logs


        //🞍 SubModels
        // maybe refactor later
        let textBox_Msg (id, msg) m =
            let updateComponent c =
                match c with
                | TextBox tb when tb.Id = id -> TextBox(TextBox.update msg tb)
                | other -> other

            { m with Components = List.map updateComponent m.Components }


        let checkBox_Msg (id, msg) m =
            let updateComponent c =
                match c with
                | CheckBox cb when cb.Id = id -> CheckBox(CheckBox.update msg cb)
                | other -> other

            { m with Components = List.map updateComponent m.Components }


        let comboBox_Msg (id, msg) m =
            let updateComponent c =
                match c with
                | ComboBox cb when cb.Id = id -> ComboBox(ComboBox.update msg cb)
                | other -> other

            { m with Components = List.map updateComponent m.Components }


    let update msg m : Model * Cmd<Msg> =
        match msg with
        | SelectFormComponent id ->
            let m' = selectFormComponent id m
            m', Cmd.none
        | AddTextBox ->
            let m' = addTextBox m
            m', Cmd.none
        | AddCheckBox ->
            let m' = addCheckBox m
            m', Cmd.none
        | AddComboBox ->
            let m' = addComboBox m
            m', Cmd.none
        | RemoveComponent id ->
            let m', newSelectedId, logs = removeComponent id m
            let cmd = Cmd.ofMsg (SelectFormComponent newSelectedId)
            let m'' = { m' with Log = logs @ m'.Log }
            m'', cmd
        | ClearLog -> { m with Log = [] }, Cmd.none
        // SubModels
        | TextBox_Msg (id, msg) ->
            let m' = textBox_Msg (id, msg) m
            m', Cmd.none
        | CheckBox_Msg (id, msg) ->
            let m' = checkBox_Msg (id, msg) m
            m', Cmd.none
        | ComboBox_Msg (id, msg) ->
            let m' = comboBox_Msg (id, msg) m
            m', Cmd.none


[<AllowNullLiteral>]
type TextBox_VM(args) =
    inherit ViewModelBase<TextBox.Model, TextBox.Msg>(args)

    //• helpers

    //🅦 TextBox add ctor? TRY MAYBE IT DOEST WORK BECAUSE OF THIS
    new() = TextBox_VM(TextBox.init () |> ViewModelArgs.simple)

    //• members
    member _.Text = base.Get () (Binding.oneWay ((fun m -> m.Text)))


[<AllowNullLiteral>]
type CheckBox_VM(args) =
    inherit ViewModelBase<CheckBox.Model, CheckBox.Msg>(args)

    //• helpers
    new() = CheckBox_VM(CheckBox.init () |> ViewModelArgs.simple)

    //• members
    member _.Label = base.Get () (Binding.oneWay ((fun m -> m.Label)))


[<AllowNullLiteral>]
type ComboBox_VM(args) =
    inherit ViewModelBase<ComboBox.Model, ComboBox.Msg>(args)

    new() = ComboBox_VM(ComboBox.init () |> ViewModelArgs.simple)

    member _.Items = base.Get () (Binding.oneWay (fun m -> m.Items))
    member _.Header = base.Get () (Binding.oneWay (fun m -> m.Header))

    member _.SelectedItem
        with get () =
            base.Get
                ()
                (Binding.twoWay ((fun (m: ComboBox.Model) -> m.SelectedItem), (fun v _ -> ComboBox.Msg.SelectItem v)))
        and set (value) =
            base.Set
                (value)
                (Binding.twoWay ((fun (m: ComboBox.Model) -> m.SelectedItem), (fun v _ -> ComboBox.Msg.SelectItem v)))


[<AllowNullLiteral>]
type FormComponent_VM(args: ViewModelArgs<App.Model * App.FormComponent, App.Msg>) =
    inherit ViewModelBase<App.Model * App.FormComponent, App.Msg>(args)

    member this.CurrentModel =
        (this :> IViewModel<App.Model * App.FormComponent, App.Msg>)
            .CurrentModel

    member this.Model = fst this.CurrentModel
    member this.FormComponent = snd this.CurrentModel

    member this.Id =
        match this.FormComponent with
        | App.FormComponent.TextBox tb -> tb.Id
        | App.FormComponent.CheckBox cb -> cb.Id
        | App.FormComponent.ComboBox cb -> cb.Id

    member this.ComponentVM: obj =
        let id = this.Id

        match this.FormComponent with
        | App.FormComponent.TextBox tb ->
            upcast TextBox_VM(ViewModelArgs.map (fun _ -> tb) (fun msg -> App.Msg.TextBox_Msg(id, msg)) args)
        | App.FormComponent.CheckBox cb ->
            upcast CheckBox_VM(ViewModelArgs.map (fun _ -> cb) (fun msg -> App.Msg.CheckBox_Msg(id, msg)) args)
        | App.FormComponent.ComboBox cb ->
            upcast ComboBox_VM(ViewModelArgs.map (fun _ -> cb) (fun msg -> App.Msg.ComboBox_Msg(id, msg)) args)

    member this.SelectedLabel =
        let componentId = this.Id

        base.Get
            ()
            (Binding.oneWay (fun (m, _) ->
                if Some componentId = m.SelectedFormComponent then
                    " • Selected"
                else
                    ""))


    // can be done in XAML using lots of boilerplate
    // I guess that since it's in a ViewModel, there is no "separation of concerns" issues
    //# what do you think?
    member this.BackgroundColor: Brush =
        match this.FormComponent with
        | App.FormComponent.TextBox _ -> Brushes.DarkGreen
        | App.FormComponent.CheckBox _ -> Brushes.DarkRed
        | App.FormComponent.ComboBox _ -> Brushes.DarkOrange


// Adjusted App_VM with SelectedComponent having both get and set
[<AllowNullLiteral>]
type App_VM(args) =
    inherit ViewModelBase<App.Model, App.Msg>(args)

    let getId ((_, fc): App.Model * App.FormComponent) =
        match fc with
        | App.FormComponent.TextBox tb -> tb.Id
        | App.FormComponent.CheckBox cb -> cb.Id
        | App.FormComponent.ComboBox cb -> cb.Id


    new() = App_VM((App.init () |> fst) |> ViewModelArgs.simple)


    member _.Components_VM =
        base.Get
            ()
            (Binding.SubModelSeqKeyedT.id (fun args -> FormComponent_VM(args)) getId
             >> Binding.mapModel (fun (m: App.Model) -> m.Components |> List.map (fun fc -> (m, fc)))
             >> Binding.addLazy (fun m1 m2 ->
                 m1.SelectedFormComponent = m2.SelectedFormComponent
                 && m1.Components = m2.Components)
             >> Binding.mapMsg (fun (_id, msg) -> msg))



    member _.SelectedComponent
        with get () =
            base.Get
                ()
                (Binding.subModelSelectedItem (
                    "Components_VM",
                    (fun (m: App.Model) -> m.SelectedFormComponent),
                    App.Msg.SelectFormComponent
                ))
        and set value =
            base.Set
                (value)
                (Binding.subModelSelectedItem (
                    "Components_VM",
                    (fun (m: App.Model) -> m.SelectedFormComponent),
                    App.Msg.SelectFormComponent
                ))



    member _.AddTextBox = base.Get () (Binding.CmdT.setAlways App.Msg.AddTextBox)
    member _.AddCheckBox = base.Get () (Binding.CmdT.setAlways App.Msg.AddCheckBox)
    member _.AddComboBox = base.Get () (Binding.CmdT.setAlways App.Msg.AddComboBox)
    member _.ClearLog = base.Get () (Binding.CmdT.setAlways App.Msg.ClearLog)


    member _.RemoveSelectedComponent =
        base.Get
            ()
            (Binding.cmdIf (fun (m: App.Model) ->
                match m.SelectedFormComponent with
                | Some id -> App.Msg.RemoveComponent id |> ValueSome
                | None -> ValueNone))



    //🅦 review it (put in update?)
    member _.Log =
        base.Get
            ()
            (Binding.oneWay (fun m ->
                let logLength = List.length m.Log
                let takeCount = min 40 logLength
                String.concat "\n" (List.take takeCount m.Log)))


module Program =

    let main (window) =

        let logger =
            LoggerConfiguration()
                .MinimumLevel.Override("Elmish.WPF.Update", Events.LogEventLevel.Verbose)
                .MinimumLevel.Override("Elmish.WPF.Bindings", Events.LogEventLevel.Verbose)
                .MinimumLevel.Override("Elmish.WPF.Performance", Events.LogEventLevel.Verbose)
                .WriteTo.Console()
                .CreateLogger()

        WpfProgram.mkProgramT App.init App.update App_VM
        |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))
        |> WpfProgram.startElmishLoop window
