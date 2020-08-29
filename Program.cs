using System;
using System.Windows;

namespace FrontOfficeV
{
    public class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Application app = new Application();
            Window mainWindow = new MainWindow();
            Models.FrontOffice.entryPoint(mainWindow);
        }
    }
}
