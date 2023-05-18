module Elmish.WPF.Samples.NewWindow.Program

open System
open System.Windows

open Serilog
open Serilog.Extensions.Logging

open Elmish.WPF

open AppModule


let main (mainWindow: Window) (createWindow1: Func<#Window>) (createWindow2: Func<#Window>) : unit =
  let logger =
    LoggerConfiguration()
      .MinimumLevel.Override("Elmish.WPF.Update", Events.LogEventLevel.Verbose)
      .MinimumLevel.Override("Elmish.WPF.Bindings", Events.LogEventLevel.Verbose)
      .MinimumLevel.Override("Elmish.WPF.Performance", Events.LogEventLevel.Verbose)
      .WriteTo.Console()
      .CreateLogger()
  let createWindow1 unit : #Window = createWindow1.Invoke()
  let createWindow2 unit : #Window =
    let window = createWindow2.Invoke()
    window.Owner <- mainWindow
    window

  let init unit : App = App.init
  let bindings : (unit -> Binding<App,AppMsg> list) = App.bindings createWindow1 createWindow2
  WpfProgram.mkSimple init App.update bindings
  |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))
  |> WpfProgram.startElmishLoop mainWindow