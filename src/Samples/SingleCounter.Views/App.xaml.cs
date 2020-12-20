using System.Windows;
using System.Windows.Controls;

namespace Elmish.WPF.Samples.SingleCounter
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            EventManager.RegisterClassHandler(
                typeof(TextBox),
                UIElement.GotFocusEvent,
                new RoutedEventHandler(this.TextBox_GotFocus));
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            (sender as TextBox)?.SelectAll();
        }
    }
}
