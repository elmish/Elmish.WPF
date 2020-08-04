using System;
using Elmish.WPF.Samples.FileDialogs;
using static Elmish.WPF.Samples.FileDialogs.Program;

namespace FileDialogs.Views {
  public static class Program {
    [STAThread]
    public static void Main() =>
      main(new MainWindow());
  }
}
