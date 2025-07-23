namespace Elmish.WPF

/// <summary>
/// Represents the state of a window with an associated model
/// </summary>
/// <typeparam name="'model">The type of model associated with the window</typeparam>
[<RequireQualifiedAccess>]
type WindowState<'model> =
    /// <summary>Window is closed and has no model</summary>
    | Closed
    /// <summary>Window exists but is hidden with the given model</summary>
    | Hidden of 'model
    /// <summary>Window is visible with the given model</summary>
    | Visible of 'model


module WindowState =

    /// <summary>
    /// Catamorphism for WindowState. Applies one of three functions based on the state.
    /// </summary>
    /// <param name="a">Value to return for Closed state</param>
    /// <param name="f">Function to apply to Hidden state's model</param>
    /// <param name="g">Function to apply to Visible state's model</param>
    /// <returns>Result of applying the appropriate function</returns>
    let cata a f g =
        function
        | WindowState.Closed -> a
        | WindowState.Hidden a -> a |> f
        | WindowState.Visible a -> a |> g

    /// <summary>
    /// Maps the model inside a WindowState
    /// </summary>
    /// <param name="f">Function to transform the model</param>
    /// <returns>New WindowState with transformed model</returns>
    let map f =
        cata WindowState.Closed (f >> WindowState.Hidden) (f >> WindowState.Visible)

    /// <summary>
    /// Sets the model in a WindowState, preserving the window visibility state
    /// </summary>
    /// <param name="a">The new model value</param>
    /// <returns>WindowState with the new model</returns>
    let set a = map (fun _ -> a)

    /// <summary>
    /// Converts any WindowState to Hidden with the given model
    /// </summary>
    /// <param name="a">The model for the hidden state</param>
    /// <returns>WindowState.Hidden with the given model</returns>
    let toHidden a =
        cata (WindowState.Hidden a) WindowState.Hidden WindowState.Hidden

    /// <summary>
    /// Converts any WindowState to Visible with the given model
    /// </summary>
    /// <param name="a">The model for the visible state</param>
    /// <returns>WindowState.Visible with the given model</returns>
    let toVisible a =
        cata (WindowState.Visible a) WindowState.Visible WindowState.Visible

    /// <summary>
    /// Converts WindowState to Option, where Closed becomes None
    /// </summary>
    /// <param name="state">The WindowState to convert</param>
    /// <returns>Some(model) for Hidden/Visible states, None for Closed</returns>
    let toOption state = state |> cata None Some Some

    /// <summary>
    /// Converts WindowState to ValueOption, where Closed becomes ValueNone
    /// </summary>
    /// <param name="state">The WindowState to convert</param>
    /// <returns>ValueSome(model) for Hidden/Visible states, ValueNone for Closed</returns>
    let toVOption state =
        state |> cata ValueNone ValueSome ValueSome

    /// <summary>
    /// Converts None to WindowState.Closed, and Some(x) to WindowState.Visible(x)
    /// </summary>
    /// <param name="model">The optional model</param>
    /// <returns>WindowState.Closed for None, WindowState.Visible for Some</returns>
    let ofOption (model: 'model option) =
        match model with
        | Some a -> a |> WindowState.Visible
        | None -> WindowState.Closed

    /// <summary>
    /// Converts ValueNone to WindowState.Closed, and ValueSome(x) to WindowState.Visible(x)
    /// </summary>
    /// <param name="model">The value optional model</param>
    /// <returns>WindowState.Closed for ValueNone, WindowState.Visible for ValueSome</returns>
    let ofVOption (model: 'model voption) =
        match model with
        | ValueSome a -> a |> WindowState.Visible
        | ValueNone -> WindowState.Closed