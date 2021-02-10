using System;
using System.Windows;

namespace Elmish.WPF.Samples.FileDialogs.CmdMsg
{
  public partial class App : Application
  {
    public App()
    {
      this.Activated += StartElmish;
    }

    private void StartElmish(object sender, EventArgs e)
    {
      this.Activated -= StartElmish;
      Program.main(MainWindow);
    }

  }
}
