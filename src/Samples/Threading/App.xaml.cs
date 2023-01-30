using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Elmish.WPF.Samples.Threading;

#nullable enable
namespace Threading {
  public partial class App : Application {
    Thread? ElmishThread { get; set; } = null;

    public App() {
      this.Activated += StartElmish;
      this.Exit += StopElmish;
    }

    private void StopElmish(object? sender, ExitEventArgs e)
    {
      Dispatcher.FromThread(ElmishThread)?.InvokeShutdown();
      ElmishThread?.Join();
    }

    private void StartElmish(object? _1, EventArgs _2) {
      this.Activated -= StartElmish;
      ElmishThread = Program.main(MainWindow);
    }
  }
}