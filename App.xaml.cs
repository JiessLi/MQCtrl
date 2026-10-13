using System.Configuration;
using System.Data;
using System.Windows;
using Velopack;

namespace MQCtrl;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private System.Threading.Mutex? _singleInstance;

    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        var startInBackground = e.Args.Any(argument =>
            string.Equals(argument, "-S", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(argument, "-SILENT", StringComparison.OrdinalIgnoreCase));
        _singleInstance = new System.Threading.Mutex(true, "Local\\MQCtrl-SingleInstance", out var createdNew);
        if (!createdNew)
        {
            if (!startInBackground)
                System.Windows.MessageBox.Show("MQCtrl 已在系统托盘中运行。", "MQCtrl", MessageBoxButton.OK, MessageBoxImage.Information);
            _singleInstance.Dispose();
            _singleInstance = null;
            Shutdown();
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        base.OnStartup(e);
        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        if (!startInBackground) mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.ReleaseMutex();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}

