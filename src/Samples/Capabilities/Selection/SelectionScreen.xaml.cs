using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace Elmish.WPF.Samples.Capabilities {
  public partial class SelectionScreen : UserControl {
    public SelectionScreen() {
      InitializeComponent();
    }

    private void Hyperlink_RequestNavigate(object _, RequestNavigateEventArgs e) {
      _ = Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
      e.Handled = true;
    }

  }
}