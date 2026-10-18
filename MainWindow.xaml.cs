using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using MQCtrl.Models;
using MQCtrl.Services;
using Forms = System.Windows.Forms;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using MessageBox = System.Windows.MessageBox;

namespace MQCtrl;

public partial class MainWindow : Window
{
    private readonly ConfigStore _store = new();
    private readonly MqttConnectionService _mqtt = new();
    private readonly UpdateService _updates = new();
    private readonly DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromMinutes(10) };
    private readonly string _logFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MQCtrl", "logs.jsonl");
    private readonly Forms.NotifyIcon _trayIcon;
    private readonly System.Drawing.Icon _trayIconImage;
    private AppConfig _config;
    private bool _initialized;
    private bool _reallyExit;

    public ObservableCollection<CommandFeature> Features { get; } = [];
    public ObservableCollection<LogEntry> Logs { get; } = [];
    public ObservableCollection<object> LogItems { get; } = [];

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        LoadRecentLogs();
        AddLog("MQCtrl", "程序已启动", "info");
        try
        {
            _config = _store.Load();
            AddLog("MQCtrl", $"配置文件：{_store.Path}", "info");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法读取配置文件：\n{ex.Message}\n\n将使用空配置启动。", "MQCtrl", MessageBoxButton.OK, MessageBoxImage.Error);
            _config = new AppConfig();
            AddLog("配置读取失败", ex.Message, "error");
        }

        foreach (var feature in _config.Features) Features.Add(feature);
        if (_config.StartWithWindows)
        {
            try { EnsureWindowsStartup(); }
            catch (Exception ex) { AddLog("开机自启动检查失败", ex.Message, "error"); }
        }
        StartupToggle.IsChecked = _config.StartWithWindows;
        UpdateStartupLabel();
        PlatformBadge.Text = _config.Mqtt.Platform == "Generic" ? "通用 MQTT" : "巴法云";

        _trayIconImage = CreateTrayIcon();
        _trayIcon = new Forms.NotifyIcon
        {
            Icon = _trayIconImage,
            Text = "MQCtrl · MQTT 电脑控制",
            Visible = true,
            ContextMenuStrip = new Forms.ContextMenuStrip()
        };
        _trayIcon.ContextMenuStrip.Items.Add("打开界面", null, (_, _) => ShowMainWindow());
        _trayIcon.ContextMenuStrip.Items.Add("退出", null, (_, _) => ExitApplication());
        _trayIcon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) ShowMainWindow(); };

        _mqtt.StatusChanged += status => Dispatcher.BeginInvoke(() => SetConnectionStatus(status));
        _mqtt.Log += text => Dispatcher.BeginInvoke(() => AddLog("MQTT", text, "info"));
        _mqtt.MessageReceived += message => Dispatcher.BeginInvoke(async () => await HandleMessageAsync(message));
        _updates.Log += (title, detail) => Dispatcher.BeginInvoke(() => AddLog(title, detail, "muted"));
        _updates.StatusChanged += status => Dispatcher.BeginInvoke(() =>
            UpdateStatusText.Text = $"v{_updates.CurrentVersion} | {FormatUpdateStatus(status)}");
        _updateTimer.Tick += async (_, _) => await _updates.CheckAndDownloadAsync();
        _updateTimer.Start();
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
        ApplySystemTheme();
        _initialized = true;
        RefreshMqtt();
        _ = _updates.CheckAndDownloadAsync();
        if (string.IsNullOrWhiteSpace(_config.Mqtt.ClientId))
            AddLog("需要配置 MQTT", "点击右上角齿轮填写巴法云客户端 ID（用户私钥）", "muted");
        if (Features.All(x => !x.Enabled))
            AddLog("示例指令已添加", "示例默认关闭；编辑主题和动作后，可在功能卡片上启用", "muted");
    }

    private async void RefreshMqtt()
    {
        await _mqtt.StartAsync(_config.Mqtt, Features.Where(x => x.Enabled).Select(x => x.Topic));
    }

    private void SetConnectionStatus(string status)
    {
        HeaderStatus.Text = status;
        var online = status == "在线";
        HeaderStatus.Foreground = online ? (MediaBrush)FindResource("SuccessBrush") : (MediaBrush)FindResource("SecondaryText");
        HeaderDot.Fill = online ? (MediaBrush)FindResource("SuccessBrush") : (MediaBrush)FindResource("SecondaryText");
    }

    private async Task HandleMessageAsync(IncomingMqttMessage message)
    {
        AddLog("收到 MQTT 消息", $"主题：{message.Topic}　内容：{message.Payload}", "info");
        var feature = Features.FirstOrDefault(x => x.Enabled && string.Equals(x.Topic, message.Topic, StringComparison.Ordinal) && string.Equals(x.MatchValue.Trim(), message.Payload.Trim(), StringComparison.OrdinalIgnoreCase));
        if (feature is null)
        {
            AddLog("未匹配指令", "未找到启用且匹配的功能", "muted");
            return;
        }
        AddLog("执行指令", feature.Name, "info");
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var result = await CommandExecutor.ExecuteAsync(feature);
        AddLog(result.StartsWith("执行失败", StringComparison.Ordinal) || result.StartsWith("失败", StringComparison.Ordinal) ? "执行失败" : "执行结果", $"{feature.Name}：{result}", result.StartsWith("执行失败", StringComparison.Ordinal) ? "error" : "success");
    }

    private void AddLog(string title, string detail, string kind)
    {
        var now = DateTime.Now;
        var timestamp = now.ToString("O");
        var entry = new LogEntry(now, title, detail, kind);
        Logs.Insert(0, entry);
        while (Logs.Count > 200) Logs.RemoveAt(Logs.Count - 1);
        RefreshLogItems();
        if (LogItems.Count > 0) LogList.ScrollIntoView(LogItems[0]);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath)!);
            using (var stream = new FileStream(_logFilePath, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
            {
                using var writer = new StreamWriter(stream);
                writer.WriteLine(JsonSerializer.Serialize(new PersistedLogEntry(timestamp, title, detail, kind)));
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            TrimLogFileIfNeeded();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Debug.WriteLine($"无法写入 MQCtrl 日志：{ex.Message}");
        }
    }

    private void TrimLogFileIfNeeded()
    {
        var file = new FileInfo(_logFilePath);
        if (file.Length <= 512 * 1024) return;

        var temporaryPath = _logFilePath + ".tmp";
        try
        {
            File.WriteAllLines(temporaryPath, File.ReadLines(_logFilePath).TakeLast(200), new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, _logFilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private void LoadRecentLogs()
    {
        try
        {
            if (!File.Exists(_logFilePath)) return;
            foreach (var line in File.ReadLines(_logFilePath).TakeLast(200))
            {
                try
                {
                    var entry = JsonSerializer.Deserialize<PersistedLogEntry>(line);
                    if (entry is null) continue;
                    var timestamp = DateTime.TryParse(entry.Timestamp, out var time) ? time : DateTime.MinValue;
                    Logs.Add(new LogEntry(timestamp, entry.Title, entry.Detail, entry.Kind));
                }
                catch (JsonException) { }
            }
            var chronological = Logs.ToArray();
            Logs.Clear();
            foreach (var entry in chronological) Logs.Insert(0, entry);
            RefreshLogItems();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Debug.WriteLine($"无法读取 MQCtrl 日志：{ex.Message}");
        }
    }

    private void RefreshLogItems()
    {
        LogItems.Clear();
        DateTime? previousDate = null;
        foreach (var entry in Logs)
        {
            if (previousDate != entry.Timestamp.Date)
            {
                LogItems.Add(new LogDateHeader(entry.DateLabel));
                previousDate = entry.Timestamp.Date;
            }
            LogItems.Add(entry);
        }
    }

    private void EditMqtt_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ConfigDialog(_config.Mqtt) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        _config.Mqtt = dialog.Result;
        PlatformBadge.Text = _config.Mqtt.Platform == "Generic" ? "通用 MQTT" : "巴法云";
        SaveConfig();
        RefreshMqtt();
    }

    private void AddFeature_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new FeatureDialog { Owner = this };
        if (dialog.ShowDialog() != true) return;
        Features.Add(dialog.Result);
        _config.Features = Features.ToList();
        SaveConfig();
        RefreshMqtt();
    }

    private void EditFeature_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not CommandFeature feature) return;
        var dialog = new FeatureDialog(feature) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        if (dialog.DeleteRequested) Features.Remove(feature);
        _config.Features = Features.ToList();
        FeatureItemsRefresh();
        SaveConfig();
        RefreshMqtt();
    }

    private void FeatureEnabled_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized) return;
        _config.Features = Features.ToList();
        FeatureItemsRefresh();
        SaveConfig();
        RefreshMqtt();
    }

    private void FeatureItemsRefresh()
    {
        var items = Features.ToList();
        Features.Clear();
        foreach (var item in items) Features.Add(item);
    }

    private void StartupToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (!_initialized || StartupToggle.IsChecked is null) return;
        var enabled = StartupToggle.IsChecked == true;
        try
        {
            SetWindowsStartup(enabled);
            _config.StartWithWindows = enabled;
            SaveConfig();
            UpdateStartupLabel();
            AddLog("开机自启动", enabled ? "已启用" : "已关闭", "info");
        }
        catch (Exception ex)
        {
            StartupToggle.IsChecked = !enabled;
            MessageBox.Show(this, $"修改开机自启动失败：{ex.Message}", "MQCtrl", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void SetWindowsStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true)
            ?? Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
        const string name = "MQCtrl";
        if (!enabled) { key.DeleteValue(name, false); return; }
        key.SetValue(name, GetStartupCommand());
    }

    private static void EnsureWindowsStartup()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: false);
        if (key?.GetValue("MQCtrl") as string != GetStartupCommand()) SetWindowsStartup(true);
    }

    private static string GetStartupCommand()
    {
        var executable = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException("无法获取程序路径");
        if (string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            var assembly = Environment.GetCommandLineArgs().Skip(1)
                .FirstOrDefault(argument => argument.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && File.Exists(argument));
            if (!string.IsNullOrWhiteSpace(assembly)) return $"\"{executable}\" \"{assembly}\" -S";
        }
        return $"\"{executable}\" -S";
    }

    private void UpdateStartupLabel()
    {
        StartupLabel.Text = _config.StartWithWindows ? "已开启" : "已关闭";
        StartupLabel.Foreground = _config.StartWithWindows ? (MediaBrush)FindResource("SuccessBrush") : (MediaBrush)FindResource("SecondaryText");
    }

    private void SaveConfig()
    {
        try { _store.Save(_config); }
        catch (Exception ex) { AddLog("配置保存失败", ex.Message, "error"); MessageBox.Show(this, $"保存配置失败：{ex.Message}", "MQCtrl", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        Logs.Clear();
        LogItems.Clear();
        try { File.Delete(_logFilePath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            AddLog("日志清理失败", ex.Message, "error");
        }
    }

    private async void UpdateStatus_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_updates.IsInstalled)
        {
            await _updates.CheckAndDownloadAsync();
            return;
        }

        if (!_updates.HasUpdate)
        {
            await _updates.CheckAndDownloadAsync();
            return;
        }

        var result = MessageBox.Show(this,
            $"检测到新版本 {_updates.AvailableVersion}，是否立即更新并重新启动 MQCtrl？\n\n选择“否”会在下次启动时自动应用更新。",
            "MQCtrl 更新", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        await _mqtt.DisposeAsync();
        _trayIcon.Visible = false;
        _updates.ApplyPendingUpdateAndRestart();
    }

    private static string FormatUpdateStatus(string status) => status switch
    {
        "无需更新" => "最新版",
        "等待更新" => "待更新",
        "正在检查" => "检查中",
        "无法检查更新" => "无法检查",
        "检查失败" => "无法检查",
        _ => status
    };

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseToTray_Click(object sender, RoutedEventArgs e) => Close();

    internal void ShowMainWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_reallyExit) return;
        e.Cancel = true;
        Hide();
        _trayIcon.ShowBalloonTip(1800, "MQCtrl 仍在运行", "程序已最小化到系统托盘，继续监听 MQTT 消息。", Forms.ToolTipIcon.Info);
    }

    private void ExitApplication()
    {
        _reallyExit = true;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
        _mqtt.Stop();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _trayIconImage.Dispose();
        Close();
        System.Windows.Application.Current.Shutdown();
    }

    private static System.Drawing.Icon CreateTrayIcon()
    {
        var logo = (DrawingImage)System.Windows.Application.Current.FindResource("MqCtrlLogo");
        const int renderSize = 256;
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
            context.DrawImage(logo, new Rect(0, 0, renderSize, renderSize));

        var renderedLogo = new RenderTargetBitmap(renderSize, renderSize, 96, 96, PixelFormats.Pbgra32);
        renderedLogo.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(renderedLogo));
        using var pngStream = new MemoryStream();
        encoder.Save(pngStream);
        pngStream.Position = 0;
        using var source = new System.Drawing.Bitmap(pngStream);
        using var trayBitmap = new System.Drawing.Bitmap(source, new System.Drawing.Size(32, 32));
        var handle = trayBitmap.GetHicon();
        try { return (System.Drawing.Icon)System.Drawing.Icon.FromHandle(handle).Clone(); }
        finally { DestroyIcon(handle); }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    private void ApplySystemTheme()
    {
        var light = true;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            light = Convert.ToInt32(key?.GetValue("AppsUseLightTheme", 1)) != 0;
        }
        catch { }
        var values = light
            ? new Dictionary<string, string> { ["WindowBackground"] = "#F3F3F3", ["PanelBackground"] = "#FAFAFA", ["CardBackground"] = "#FFFFFF", ["FieldBackground"] = "#FFFFFF", ["BorderBrush"] = "#E5E5E5", ["PrimaryText"] = "#1A1A1A", ["SecondaryText"] = "#616161", ["AccentBrush"] = "#0067C0", ["SuccessBrush"] = "#107C10", ["DangerBrush"] = "#C42B1C", ["ToggleOffBrush"] = "#C7C7C7", ["ButtonHoverBrush"] = "#E5E5E5", ["ButtonPressedBrush"] = "#D6D6D6" }
            : new Dictionary<string, string> { ["WindowBackground"] = "#202020", ["PanelBackground"] = "#2B2B2B", ["CardBackground"] = "#303030", ["FieldBackground"] = "#252525", ["BorderBrush"] = "#424242", ["PrimaryText"] = "#FFFFFF", ["SecondaryText"] = "#C5C5C5", ["AccentBrush"] = "#60CDFF", ["SuccessBrush"] = "#6CCB5F", ["DangerBrush"] = "#FF99A4", ["ToggleOffBrush"] = "#5B5B5B", ["ButtonHoverBrush"] = "#3A3A3A", ["ButtonPressedBrush"] = "#454545" };
        foreach (var (name, color) in values)
            System.Windows.Application.Current.Resources[name] = new SolidColorBrush((MediaColor)System.Windows.Media.ColorConverter.ConvertFromString(color));
    }

    private void SystemEvents_UserPreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        if (e.Category == Microsoft.Win32.UserPreferenceCategory.General)
            Dispatcher.BeginInvoke(ApplySystemTheme, DispatcherPriority.Background);
    }
}

public sealed record LogDateHeader(string DateLabel);

public sealed class LogEntry
{
    public DateTime Timestamp { get; }
    public string DateLabel => Timestamp == DateTime.MinValue ? "日期未知" : Timestamp.ToString("yyyy年M月d日");
    public string Time => Timestamp == DateTime.MinValue ? "--:--:--" : Timestamp.ToString("HH:mm:ss");
    public string Title { get; }
    public string Detail { get; }
    public MediaBrush DotBrush { get; }
    public MediaBrush TitleBrush { get; }
    public LogEntry(DateTime timestamp, string title, string detail, string kind)
    {
        Timestamp = timestamp; Title = title; Detail = detail;
        var color = kind switch { "success" => "#30E891", "error" => "#FF5C73", "muted" => "#8393A7", _ => "#21C9F4" };
        DotBrush = new SolidColorBrush((MediaColor)System.Windows.Media.ColorConverter.ConvertFromString(color));
        TitleBrush = kind == "success" ? new SolidColorBrush((MediaColor)System.Windows.Media.ColorConverter.ConvertFromString("#30E891")) : kind == "error" ? new SolidColorBrush((MediaColor)System.Windows.Media.ColorConverter.ConvertFromString("#FF5C73")) : new SolidColorBrush((MediaColor)System.Windows.Media.ColorConverter.ConvertFromString("#21C9F4"));
    }
}

internal sealed record PersistedLogEntry(string Timestamp, string Title, string Detail, string Kind);
