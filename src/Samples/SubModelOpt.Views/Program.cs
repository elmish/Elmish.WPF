using System;
using Elmish.WPF.Samples.SubModelOpt;
using static Elmish.WPF.Samples.SubModelOpt.Program;

namespace SubModelOpt.Views {
  public static class Program {
    [STAThread]
    public static void Main() =>
      main(new MainWindow());
  }
}
