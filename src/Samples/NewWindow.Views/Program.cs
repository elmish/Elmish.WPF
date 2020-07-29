using System;
using Elmish.WPF.Samples.NewWindow;
using static Elmish.WPF.Samples.NewWindow.Program;

namespace NewWindow.Views {
  public static class Program {
    [STAThread]
    public static void Main() =>
      main(new MainWindow(), () => new Window1(), () => new Window2());
  }
}
