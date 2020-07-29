using System;
using Elmish.WPF.Samples.FileDialogs.CmdMsg;
using static Elmish.WPF.Samples.FileDialogs.CmdMsg.Program;

namespace FileDialogs.CmdMsg.Views {
  public static class Program {
    [STAThread]
    public static void Main() =>
      main(new MainWindow());
  }
}
