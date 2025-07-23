#nullable enable
using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Elmish.WPF.Samples.Threading;

namespace Threading;

public partial class App : Application
{
    public App()
    {
        Activated += StartElmish;
        Exit += StopElmish;
    }

    private Thread? ElmishThread { get; set; }

    private void StopElmish(object? sender, ExitEventArgs e)
    {
        Dispatcher.FromThread(ElmishThread)?.InvokeShutdown();
        ElmishThread?.Join();
    }

    private void StartElmish(object? _1, EventArgs _2)
    {
        Activated -= StartElmish;
        ElmishThread = Program.main(MainWindow);
    }
}