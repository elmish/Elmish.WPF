module Elmish.WPF.Samples.NewWindow.Program

open System
open System.Windows

open Serilog
open Serilog.Extensions.Logging

open Elmish.WPF

open AppModule


let main mainWindow (createWindow1: Func<#Window>) (createWindow2: Func<#Window>) =
  let logger =
    LoggerConfiguration()
      .MinimumLevel.Override("Elmish.WPF.Update", Events.LogEventLevel.Verbose)
      .MinimumLevel.Override("Elmish.WPF.Bindings", Events.LogEventLevel.Verbose)
      .MinimumLevel.Override("Elmish.WPF.Performance", Events.LogEventLevel.Verbose)
      .WriteTo.Console()
      .CreateLogger()
  let createWindow1 () = createWindow1.Invoke()
  let createWindow2 () =
    let window = createWindow2.Invoke()
    window.Owner <- mainWindow
    window

  let init () = App.init
  let bindings = App.bindings createWindow1 createWindow2
  WpfProgram.mkSimple init App.update bindings
  |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))
  |> WpfProgram.startElmishLoop mainWindow