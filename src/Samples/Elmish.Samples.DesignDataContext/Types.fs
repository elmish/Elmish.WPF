namespace Elmish.Samples.DesignDataContext

type DialogModel =
    { IsVisible: bool
      Result: bool }

type Model =
    { Name: string
      Email: string
      Age: int
      Address: string
      Phone: string
      Postal: string
      IsDialogVisible: bool
      DialogResult: bool option }

type Msg =
    | SetName of string
    | SetEmail of string
    | SetAge of string
    | SetAddress of string
    | SetPhone of string
    | SetPostal of string
    | SetDialogVisible of bool
    | SetDialogResult of bool option