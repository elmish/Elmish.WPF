using Navigation.Core;

namespace Navigation;

public partial class App
{
    public App()
    {
        Activated += StartElmish;
    }

    private void StartElmish(object? sender, EventArgs e)
    {
        Activated -= StartElmish;
        Program.run(MainWindow);
    }
}