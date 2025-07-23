using System.Threading.Tasks;
using System.Windows;

namespace Elmish.WPF.Samples.Threading;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        Task.Delay(5000).Wait();
    }
}