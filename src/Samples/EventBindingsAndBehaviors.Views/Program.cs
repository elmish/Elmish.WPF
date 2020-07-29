using System;
using Elmish.WPF.Samples.EventBindingsAndBehaviors;
using static Elmish.WPF.Samples.EventBindingsAndBehaviors.Program;

namespace EventBindingsAndBehaviors.Views {
  public static class Program {
    [STAThread]
    public static void Main() =>
      main(new MainWindow());
  }
}
