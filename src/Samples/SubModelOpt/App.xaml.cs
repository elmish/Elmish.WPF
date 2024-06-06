using System;
using System.Windows;

namespace Elmish.WPF.Samples.SubModelOpt;

public partial class App
{
    public App()
    {
        this.Activated += StartElmish;
    }

    private void StartElmish(object sender, EventArgs e)
    {
        this.Activated -= StartElmish;
        Program.Program.main(MainWindow);
    }

}