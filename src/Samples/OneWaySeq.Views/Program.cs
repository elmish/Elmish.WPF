using System;
using Elmish.WPF.Samples.OneWaySeq;
using static Elmish.WPF.Samples.OneWaySeq.Program;

namespace OneWaySeq.Views {
  public static class Program {
    [STAThread]
    public static void Main() =>
      main(new MainWindow());
  }
}
