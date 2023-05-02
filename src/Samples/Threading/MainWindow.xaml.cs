using System.Windows;

namespace Elmish.WPF.Samples.Threading
{
  public partial class MainWindow : Window
  {
    public MainWindow()
    {
      InitializeComponent();
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
      System.Threading.Tasks.Task.Delay(5000).Wait();
    }

    private void Button2_Click(object sender, RoutedEventArgs e)
    {
      dynamic viewModel = DataContext;
      System.Threading.Tasks.Task.Delay(5000).ContinueWith(t => viewModel.Message = $"{viewModel.Message}{viewModel.Pings}");
    }
  }
}