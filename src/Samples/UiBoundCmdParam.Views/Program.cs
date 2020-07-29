using System;
using Elmish.WPF.Samples.UiBoundCmdParam;
using static Elmish.WPF.Samples.UiBoundCmdParam.Program;

namespace UiBoundCmdParam.Views {
  public static class Program {
    [STAThread]
    public static void Main() =>
      main(new MainWindow());
  }
}
