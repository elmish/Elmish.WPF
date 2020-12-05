using System;
using Elmish.WPF.Samples.SubModelSeq.Tree;
using static Elmish.WPF.Samples.SubModelSeq.Tree.Program;

namespace SubModelSeq.Tree.Views {
  public static class Program {
    [STAThread]
    public static void Main() =>
      main(new MainWindow());
  }
}
