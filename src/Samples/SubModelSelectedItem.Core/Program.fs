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

    • refactor: make FormComponent more concrete = TextBox, CheckBox, ComboBox
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


module FormComponentA =

    type Model = { Id: Guid; Name: string }

    let create () =
        { Id = Guid.NewGuid()
          Name = FormComponentHelpers.generateName "A_" }

    let init () = create ()

    type Msg = DummyMsg

    let update msg m =
        match msg with
        | DummyMsg -> m


module FormComponentB =

    type Model = { Id: Guid; Name: string }

    let create () =
        { Id = Guid.NewGuid()
          Name = FormComponentHelpers.generateName "B_" }

    let init () =
        { Id = Guid.NewGuid()
          Name = "B_" + Random().Next(10000, 100000).ToString() }

    type Msg = DummyMsg

    let update msg m =
        match msg with
        | DummyMsg -> m


module FormComponentC =

    type Model = { Id: Guid; Name: string }

    let create () =
        { Id = Guid.NewGuid()
          Name = FormComponentHelpers.generateName "C_" }

    let init () = create ()

    type Msg = DummyMsg

    let update msg m =
        match msg with
        | DummyMsg -> m


module Form =

    type Components =
        | FormComponentA of FormComponentA.Model
        | FormComponentB of FormComponentB.Model
        | FormComponentC of FormComponentC.Model


    type Model =
        { Components: Components list
          SelectedComponent: Guid option
          //• SubModels
          FormComponentA_Model: FormComponentA.Model
          FormComponentB_Model: FormComponentB.Model
          FormComponentC_Model: FormComponentC.Model }


    let components_Mock =
        [ for _ in 1..3 do
              yield FormComponentA(FormComponentA.create ())
              yield FormComponentB(FormComponentB.create ())
              yield FormComponentC(FormComponentC.create ()) ]


    let init () =
        { Components = components_Mock
          SelectedComponent = None
          //• SubModels
          FormComponentA_Model = FormComponentA.init ()
          FormComponentB_Model = FormComponentB.init ()
          FormComponentC_Model = FormComponentC.init () }

    type Msg =
        | Select of Guid option
        | AddFormComponentA
        | AddFormComponentB
        | AddFormComponentC
        //• SubMsgs
        | TextBoxA_Msg of FormComponentA.Msg
        | TextBoxB_Msg of FormComponentB.Msg
        | TextBoxC_Msg of FormComponentC.Msg

    [<AutoOpen>]
    module Form =

        let getSelectedEntityIdFromSelectComponent (m: Model) =
            match m.SelectedComponent with
            | Some selectedId -> selectedId
            | None -> Guid.Empty

        let getComponentId component_ =
            match component_ with
            | FormComponentA a -> a.Id
            | FormComponentB b -> b.Id
            | FormComponentC c -> c.Id

        let getComponentName component_ =
            match component_ with
            | FormComponentA a -> a.Name
            | FormComponentB b -> b.Name
            | FormComponentC c -> c.Name

        let isSelected selectedId component_ =
            match selectedId, component_ with
            | Some id, FormComponentA a -> a.Id = id
            | Some id, FormComponentB b -> b.Id = id
            | Some id, FormComponentC c -> c.Id = id
            | _ -> false

    let insertComponentAfterSelected selectedComponent newComponent components =

        // sample purpose: make explicit that a new component has been added
        let prependNewName component_ =
            match component_ with
            | FormComponentA a -> FormComponentA { a with Name = "#New# " + a.Name }
            | FormComponentB b -> FormComponentB { b with Name = "#New# " + b.Name }
            | FormComponentC c -> FormComponentC { c with Name = "#New# " + c.Name }

        let newComponentWithPrependedName = prependNewName newComponent

        match selectedComponent with
        | None ->
            // If no component is selected, append the new one to the end
            components @ [ newComponentWithPrependedName ]
        | Some selectedId ->
            let rec insertAfterSelected =
                function
                | [] -> [ newComponentWithPrependedName ]
                | comp :: rest when getComponentId comp = selectedId -> comp :: newComponentWithPrependedName :: rest
                | comp :: rest -> comp :: insertAfterSelected rest

            insertAfterSelected components


    let update msg m =
        match msg with
        | Select entityId -> { m with SelectedComponent = entityId }

        | AddFormComponentA ->
            let newComponent = FormComponentA(FormComponentA.create ())

            let newComponentId =
                match newComponent with
                | FormComponentA a -> a.Id
                | _ -> Guid.Empty

            { m with
                Components = insertComponentAfterSelected m.SelectedComponent newComponent m.Components
                SelectedComponent = Some newComponentId }

        | AddFormComponentB ->
            let newComponent = FormComponentB(FormComponentB.create ())

            let newComponentId =
                match newComponent with
                | FormComponentB b -> b.Id
                | _ -> Guid.Empty

            { m with
                Components = insertComponentAfterSelected m.SelectedComponent newComponent m.Components
                SelectedComponent = Some newComponentId }

        | AddFormComponentC ->
            let newComponent = FormComponentC(FormComponentC.create ())

            let newComponentId =
                match newComponent with
                | FormComponentC c -> c.Id
                | _ -> Guid.Empty

            { m with
                Components = insertComponentAfterSelected m.SelectedComponent newComponent m.Components
                SelectedComponent = Some newComponentId }

        //• SubModels
        | TextBoxA_Msg msg -> { m with FormComponentA_Model = FormComponentA.update msg m.FormComponentA_Model }
        | TextBoxB_Msg msg -> { m with FormComponentB_Model = FormComponentB.update msg m.FormComponentB_Model }
        | TextBoxC_Msg msg -> { m with FormComponentC_Model = FormComponentC.update msg m.FormComponentC_Model }


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
                            | Form.Components.FormComponentA _ -> "Type: A"
                            | Form.Components.FormComponentB _ -> "Type: B"
                            | Form.Components.FormComponentC _ -> "Type: C"

                        sprintf "Selected: Name = %s, Index = %d, %s" name index componentType
                    | None -> "No selection"))
        and set (value) = base.Set value (Binding.oneWay (fun _ -> ""))


    //• Commands
    member _.AddTextBoxA = base.Get () (Binding.CmdT.setAlways Form.AddFormComponentA)
    member _.AddTextBoxB = base.Get () (Binding.CmdT.setAlways Form.AddFormComponentB)
    member _.AddTextBoxC = base.Get () (Binding.CmdT.setAlways Form.AddFormComponentC)

    member _.SelectRandom =
        base.Get
            ()
            (Binding.cmd (fun (m: Form.Model) ->
                let randomEntity = m.Components.Item(Random().Next(m.Components.Length))

                match randomEntity with
                | Form.Components.FormComponentA aModel -> Some aModel.Id
                | Form.Components.FormComponentB bModel -> Some bModel.Id
                | Form.Components.FormComponentC cModel -> Some cModel.Id
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
