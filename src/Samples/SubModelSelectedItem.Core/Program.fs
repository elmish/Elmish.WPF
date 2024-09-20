namespace Elmish.WPF.Samples.SubModelSelectedItem.Program

open System
open Serilog
open Serilog.Extensions.Logging
open Elmish.WPF

(*
[toDos]
    • *change all [dynamic bindings] to [static bindings] using an upcoming Elmish.WPF revised static bindings approach*
    • [?] make "_VM" for each child (cleaner separation)
    • [?] would something other than "SubModelSelectItem" be a better option for safety?
    • [?] how to better seperate *specific children fields* within dynamic bindings in "Form_VM.Components"? Just comment? Helpers?

    • add: DataTemplateSelector
    • add: get focus after adding + selecting FormComponent (Behavior)

    • refactor: revise all helpers in Form (some were made quick&dirty)
    • refactor: make update and VM cleaner (helpers)
    • revise naming(?): keep "_Model", "_Msg", "_VM"? IMO it helps seperate better childs visually + better Intellisense experience in Xaml
*)


module FormComponentHelpers =
    let generateName (prefix: string) =
        let randomNumber () = Random().Next(1000, 10000).ToString()
        prefix + randomNumber ()


module TextBoxComponent =

    type Model = { Id: Guid; Text: string }

    let create () =
        { Id = Guid.NewGuid()
          Text = FormComponentHelpers.generateName "TextBox_" }

    let init () = create ()

    type Msg = DummyMsg

    let update msg m =
        match msg with
        | DummyMsg -> m


module CheckBoxComponent =

    type Model =
        { Id: Guid
          Label: string
          IsChecked: bool }

    let create () =
        { Id = Guid.NewGuid()
          Label = FormComponentHelpers.generateName "CheckBox_"
          IsChecked = false }

    let init () = create ()

    type Msg = DummyMsg

    let update msg m =
        match msg with
        | DummyMsg -> m


module ComboBoxComponent =

    type Model =
        { Id: Guid
          Name: string // header of GroupBox containing it
          Items: string list }

    let create () =
        { Id = Guid.NewGuid()
          Name = FormComponentHelpers.generateName "ComboBox_"
          Items = [ "Item 1"; "Item 2"; "Item 3" ] }

    let init () = create ()

    type Msg = DummyMsg

    let update msg m =
        match msg with
        | DummyMsg -> m


module Form =

    type Components =
        | TextBox of TextBoxComponent.Model
        | CheckBox of CheckBoxComponent.Model
        | ComboBox of ComboBoxComponent.Model

    type Model =
        { Components: Components list
          SelectedComponent: Guid option
          //• SubModels
          TextBox_Model: TextBoxComponent.Model
          CheckBox_Model: CheckBoxComponent.Model
          ComboBox_Model: ComboBoxComponent.Model }

    let components_Mock =
        [ for _ in 1..3 do
              yield TextBox(TextBoxComponent.create ())
              yield CheckBox(CheckBoxComponent.create ())
              yield ComboBox(ComboBoxComponent.create ()) ]

    let init () =
        { Components = components_Mock
          SelectedComponent = None
          //• SubModels
          TextBox_Model = TextBoxComponent.init ()
          CheckBox_Model = CheckBoxComponent.init ()
          ComboBox_Model = ComboBoxComponent.init () }

    type Msg =
        | Select of Guid option
        | AddTextBox
        | AddCheckBox
        | AddComboBox
        //• SubMsgs
        | TextBox_Msg of TextBoxComponent.Msg
        | CheckBox_Msg of CheckBoxComponent.Msg
        | ComboBoxC_Msg of ComboBoxComponent.Msg

    [<AutoOpen>]
    module Form =

        let getSelectedEntityIdFromSelectComponent (m: Model) =
            match m.SelectedComponent with
            | Some selectedId -> selectedId
            | None -> Guid.Empty

        let getComponentId component_ =
            match component_ with
            | TextBox a -> a.Id
            | CheckBox b -> b.Id
            | ComboBox c -> c.Id

        let getComponentName component_ =
            match component_ with
            | TextBox a -> a.Text
            | CheckBox b -> b.Label
            | ComboBox c -> c.Name

        let isSelected selectedId component_ =
            match selectedId, component_ with
            | Some id, TextBox a -> a.Id = id
            | Some id, CheckBox b -> b.Id = id
            | Some id, ComboBox c -> c.Id = id
            | _ -> false

        let insertComponentAfterSelected selectedComponent newComponent components =

            // sample purpose: make explicit that a new component has been added
            let prependNewName component_ =
                match component_ with
                | TextBox a -> TextBox { a with Text = "#New# " + a.Text }
                | CheckBox b -> CheckBox { b with Label = "#New# " + b.Label }
                | ComboBox c -> ComboBox { c with Name = "#New# " + c.Name }

            let newComponentWithPrependedName = prependNewName newComponent

            match selectedComponent with
            | None ->
                // If no component is selected, append the new one to the end
                components @ [ newComponentWithPrependedName ]
            | Some selectedId ->
                let rec insertAfterSelected =
                    function
                    | [] -> [ newComponentWithPrependedName ]
                    | comp :: rest when getComponentId comp = selectedId ->
                        comp :: newComponentWithPrependedName :: rest
                    | comp :: rest -> comp :: insertAfterSelected rest

                insertAfterSelected components


    let update msg m =
        match msg with
        | Select entityId -> { m with SelectedComponent = entityId }

        | AddTextBox ->
            let newTextBox = TextBox(TextBoxComponent.create ())

            let newTextBoxId =
                match newTextBox with
                | TextBox a -> a.Id
                | _ -> Guid.Empty

            { m with
                Components = insertComponentAfterSelected m.SelectedComponent newTextBox m.Components
                SelectedComponent = Some newTextBoxId }

        | AddCheckBox ->
            let newCheckBox = CheckBox(CheckBoxComponent.create ())

            let newCheckBoxId =
                match newCheckBox with
                | CheckBox b -> b.Id
                | _ -> Guid.Empty

            { m with
                Components = insertComponentAfterSelected m.SelectedComponent newCheckBox m.Components
                SelectedComponent = Some newCheckBoxId }

        | AddComboBox ->
            let newComboBox = ComboBox(ComboBoxComponent.create ())

            let newComboBoxId =
                match newComboBox with
                | ComboBox c -> c.Id
                | _ -> Guid.Empty

            { m with
                Components = insertComponentAfterSelected m.SelectedComponent newComboBox m.Components
                SelectedComponent = Some newComboBoxId }

        //• SubModels
        | TextBox_Msg msg -> { m with TextBox_Model = TextBoxComponent.update msg m.TextBox_Model }
        | CheckBox_Msg msg -> { m with CheckBox_Model = CheckBoxComponent.update msg m.CheckBox_Model }
        | ComboBoxC_Msg msg -> { m with ComboBox_Model = ComboBoxComponent.update msg m.ComboBox_Model }


//# ViewModel/Bindings
open Form.Form // ugly

[<AllowNullLiteral>]
type Form_VM(args) =
    inherit ViewModelBase<Form.Model, Form.Msg>(args)

    new() = Form_VM(Form.init () |> ViewModelArgs.simple)

    //• Properties
    // I *really* don't like the stringly-typed nature of this binding + no Intellisense in Xaml for submodel properties
    member _.Components =
        base.Get
            ()
            (Binding.subModelSeq (
                (fun m -> m.Components),
                (fun (e) -> getComponentId e),
                (fun () ->
                    [ "Name"
                      |> Binding.oneWay (fun (_, e) -> getComponentName e)
                      "SelectedLabel"
                      |> Binding.oneWay (fun (m, e) ->
                          if isSelected m.SelectedComponent e then
                              " - Selected"
                          else
                              "") ])
            ))


    // I don't like the stringly-typed nature of this binding
    member _.SelectedEntity
        with get () =
            base.Get
                ()
                (Binding.subModelSelectedItem (
                    "Components",
                    (fun (m: Form.Model) -> m.SelectedComponent),
                    Form.Msg.Select
                ))
        and set (value) =
            base.Set
                value
                (Binding.subModelSelectedItem (
                    "Components",
                    (fun (m: Form.Model) -> m.SelectedComponent),
                    Form.Msg.Select
                ))

    member _.SelectedEntityLog
        with get () =
            base.Get
                ()
                (Binding.oneWay (fun (m: Form.Model) ->
                    match m.SelectedComponent with
                    | Some id ->
                        let index =
                            m.Components
                            |> List.findIndex (fun e -> getComponentId e = id)

                        let name =
                            m.Components
                            |> List.find (fun e -> getComponentId e = id)
                            |> getComponentName

                        let componentType =
                            match m.Components
                                  |> List.find (fun e -> getComponentId e = id)
                                with
                            | Form.Components.TextBox _ -> "Type: A"
                            | Form.Components.CheckBox _ -> "Type: B"
                            | Form.Components.ComboBox _ -> "Type: C"

                        sprintf "Selected: Name = %s, Index = %d, %s" name index componentType
                    | None -> "No selection"))
        and set (value) = base.Set value (Binding.oneWay (fun _ -> ""))


    //• Commands
    member _.AddTextBox = base.Get () (Binding.CmdT.setAlways Form.AddTextBox)
    member _.AddCheckBox = base.Get () (Binding.CmdT.setAlways Form.AddCheckBox)
    member _.AddComboBox = base.Get () (Binding.CmdT.setAlways Form.AddComboBox)

    member _.SelectRandom =
        base.Get
            ()
            (Binding.cmd (fun (m: Form.Model) ->
                let randomEntity = m.Components.Item(Random().Next(m.Components.Length))

                match randomEntity with
                | Form.Components.TextBox aModel -> Some aModel.Id
                | Form.Components.CheckBox bModel -> Some bModel.Id
                | Form.Components.ComboBox cModel -> Some cModel.Id
                |> Form.Msg.Select))

    member _.Deselect =
        base.Get () (Binding.cmd (fun (m: Form.Model) -> Form.Msg.Select None))


module Program =
    let main window =
        let logger =
            LoggerConfiguration()
                .MinimumLevel.Override("Elmish.WPF.Update", Events.LogEventLevel.Verbose)
                .MinimumLevel.Override("Elmish.WPF.Bindings", Events.LogEventLevel.Verbose)
                .MinimumLevel.Override("Elmish.WPF.Performance", Events.LogEventLevel.Verbose)
                .WriteTo.Console()
                .CreateLogger()

        WpfProgram.mkSimpleT Form.init Form.update Form_VM
        |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))
        |> WpfProgram.startElmishLoop window
