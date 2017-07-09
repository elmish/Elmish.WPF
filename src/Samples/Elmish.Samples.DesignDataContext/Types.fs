namespace Elmish.Samples.DesignDataContext

type Model =
    { Name: string
      Email: string
      Age: int
      Address: string
      Phone: string
      Postal: string }

type Msg =
    | SetName of string
    | SetEmail of string
    | SetAge of int
    | SetAddress of string
    | SetPhone of string
    | SetPostal of string