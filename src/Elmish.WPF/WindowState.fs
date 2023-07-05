namespace Elmish.WPF

[<RequireQualifiedAccess>]
type WindowState<'model> =
  | Closed
  | Hidden of 'model
  | Visible of 'model


module WindowState =

  let cata a f g =
    function
    | WindowState.Closed -> a
    | WindowState.Hidden a -> a |> f
    | WindowState.Visible a -> a |> g

  let map f =
    cata WindowState.Closed (f >> WindowState.Hidden) (f >> WindowState.Visible)

  let set a = map (fun _ -> a)

  let toHidden a =
    cata (WindowState.Hidden a) WindowState.Hidden WindowState.Hidden

  let toVisible a =
    cata (WindowState.Visible a) WindowState.Visible WindowState.Visible

  let toOption state = state |> cata None Some Some

  let toVOption state =
    state |> cata ValueNone ValueSome ValueSome

  /// Converts None to WindowState.Closed, and Some(x) to
  /// WindowState.Visible(x).
  let ofOption (model: 'model option) =
    match model with
    | Some a -> a |> WindowState.Visible
    | None -> WindowState.Closed

  /// Converts ValueNone to WindowState.Closed, and ValueSome(x) to
  /// WindowState.Visible(x).
  let ofVOption (model: 'model voption) =
    match model with
    | ValueSome a -> a |> WindowState.Visible
    | ValueNone -> WindowState.Closed