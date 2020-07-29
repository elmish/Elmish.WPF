using System;
using Elmish.WPF.Samples.Validation;
using static Elmish.WPF.Samples.Validation.Program;

namespace Validation.Views {
  public static class Program {
    [STAThread]
    public static void Main() =>
      main(new MainWindow());
  }
}
