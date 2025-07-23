using System;
using System.Windows;
using Elmish.WPF.Samples.Capabilities;

namespace Capabilities;

public partial class App : Application
{
    public App()
    {
        Activated += StartElmish;
    }

    private void StartElmish(object _1, EventArgs _2)
    {
        Activated -= StartElmish;
        Program.main(MainWindow);
    }
}