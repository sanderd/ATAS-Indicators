using System.Windows;

namespace sadnerd.io.ATAS.MockChartApp;

/// <summary>
/// Application entry point
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Always start with multi-chart view
        MainWindow = new MultiChartWindow();
        MainWindow.Show();
    }
}
