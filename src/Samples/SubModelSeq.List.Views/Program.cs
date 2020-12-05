using System;
using Elmish.WPF.Samples.SubModelSeq.List;
using static Elmish.WPF.Samples.SubModelSeq.List.Program;

namespace SubModelSeq.List.Views {
  public static class Program {
    [STAThread]
    public static void Main() =>
      main(new MainWindow());
  }
}
