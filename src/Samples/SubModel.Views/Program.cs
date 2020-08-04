using System;
using Elmish.WPF.Samples.SubModel;
using static Elmish.WPF.Samples.SubModel.Program;

namespace SubModel.Views {
  public static class Program {
    [STAThread]
    public static void Main() =>
      main(new MainWindow());
  }
}
