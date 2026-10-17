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
    private System.Threading.EventWaitHandle? _showWindowEvent;
    private volatile bool _isExiting;

    private const string SingleInstanceMutexName = "Local\\MQCtrl-SingleInstance";
    private const string ShowWindowEventName = "Local\\MQCtrl-ShowMainWindow";

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
        // Create the activation event before checking the mutex. This closes the
        // small startup race where a second launch could arrive before the first
        // instance has finished initializing its window.
        _showWindowEvent = new System.Threading.EventWaitHandle(
            false,
            System.Threading.EventResetMode.AutoReset,
            ShowWindowEventName);
        _singleInstance = new System.Threading.Mutex(true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            if (!startInBackground)
                _showWindowEvent.Set();
            _showWindowEvent.Dispose();
            _showWindowEvent = null;
            _singleInstance.Dispose();
            _singleInstance = null;
            Shutdown();
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        base.OnStartup(e);
        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        ListenForShowWindowRequest(mainWindow);
        if (!startInBackground) mainWindow.Show();
    }

    private void ListenForShowWindowRequest(MainWindow mainWindow)
    {
        _ = System.Threading.Tasks.Task.Run(() =>
        {
            while (!_isExiting)
            {
                try
                {
                    if (_showWindowEvent?.WaitOne(500) == true)
                        mainWindow.Dispatcher.BeginInvoke(mainWindow.ShowMainWindow);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _isExiting = true;
        _showWindowEvent?.Dispose();
        _showWindowEvent = null;
        _singleInstance?.ReleaseMutex();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}

