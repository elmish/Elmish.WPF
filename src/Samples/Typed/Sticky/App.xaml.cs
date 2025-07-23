using System;
using System.Windows;

namespace Elmish.WPF.Samples.Sticky;

public partial class App : Application
{
    public App()
    {
        Activated += StartElmish;
    }

    private void StartElmish(object sender, EventArgs e)
    {
        Activated -= StartElmish;
        Program.main(MainWindow);
    }
}