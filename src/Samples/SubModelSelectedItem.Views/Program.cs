using System;
using Elmish.WPF.Samples.SubModelSelectedItem;
using static Elmish.WPF.Samples.SubModelSelectedItem.Program;

namespace SubModelSelectedItem.Views {
  public static class Program {
    [STAThread]
    public static void Main() =>
      main(new MainWindow());
  }
}
