using System;
using Elmish.WPF.Samples.SubModelSeq;
using static Elmish.WPF.Samples.SubModelSeq.Program;

namespace SubModelSeq.Views {
  public static class Program {
    [STAThread]
    public static void Main() =>
      main(new MainWindow());
  }
}
