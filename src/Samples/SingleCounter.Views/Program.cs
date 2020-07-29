using System;
using Elmish.WPF.Samples.SingleCounter;
using static Elmish.WPF.Samples.SingleCounter.Program;

namespace SingleCounter.Views {
  public static class Program {
    [STAThread]
    public static void Main() =>
      main(new MainWindow());
  }
}
