using System;
using System.Windows;
using Elmish.WPF.Samples.Navigation;

namespace Navigation;

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