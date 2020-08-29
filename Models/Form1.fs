namespace Models

module Form01 =

    open System
    open Elmish
    open Elmish.WPF

    type Model =
        {
            Text: string
        }

    type Msg =
        | TextInput of string
        | Submit

    let init =
        { Text = "" }

    let update msg m =
        match msg with
        | TextInput s -> { m with Text = s }
        | Submit -> m  // handled by parent

    let bindings () : Binding<Model, Msg> list =
        [
            "Text" |> Binding.twoWay ((fun m -> m.Text), TextInput)
            "Submit" |> Binding.cmd Submit
        ]


