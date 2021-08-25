module Elmish.WPF.Samples.SingleCounter.Program

open Serilog
open Serilog.Extensions.Logging
open Elmish.WPF

type Model =
  { Count: int
    StepSize: int }

type Msg =
  | Increment
  | Decrement
  | SetStepSize of int
  | Reset
  | CompositeMsg of Msg list

let init =
  { Count = 0
    StepSize = 1 }

let canReset = (<>) init

let rec update msg m =
  match msg with
  | Increment -> { m with Count = m.Count + m.StepSize }
  | Decrement -> { m with Count = m.Count - m.StepSize }
  | SetStepSize x -> { m with StepSize = x }
  | Reset -> init
  | CompositeMsg msgs -> msgs |> List.map update |> List.fold (>>) id <| m

open FSharp.Control.Reactive
let batch (dispatch: 'a list -> unit) : 'a -> unit =
  let subject = Subject.broadcast
  let observable = subject :> System.IObservable<_>
  observable
  |> Observable.bufferCount 2
  |> Observable.map Seq.toList
  |> Observable.subscribe dispatch
  |> ignore
  subject.OnNext

let bindings () : Binding<Model, Msg> list = [
  "CounterValue" |> Binding.oneWay (fun m -> m.Count)
  "Increment" |> Binding.cmd Increment
  "Decrement" |> Binding.cmd Decrement |> Binding.alterMsgStream batch |> Binding.mapMsg CompositeMsg
  "StepSize" |> Binding.twoWay(
    (fun m -> float m.StepSize),
    int >> SetStepSize)
  "Reset" |> Binding.cmdIf(Reset, canReset)
]

let designVm = ViewModel.designInstance init (bindings ())

let main window =

  let logger =
    LoggerConfiguration()
      .MinimumLevel.Override("Elmish.WPF.Update", Events.LogEventLevel.Verbose)
      .MinimumLevel.Override("Elmish.WPF.Bindings", Events.LogEventLevel.Verbose)
      .MinimumLevel.Override("Elmish.WPF.Performance", Events.LogEventLevel.Verbose)
      .WriteTo.Console()
      .CreateLogger()

  WpfProgram.mkSimple (fun () -> init) update bindings
  |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))
  |> WpfProgram.startElmishLoop window
