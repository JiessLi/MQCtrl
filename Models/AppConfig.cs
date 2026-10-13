using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Windows.Media;

namespace MQCtrl.Models;

public sealed class AppConfig
{
    public MqttSettings Mqtt { get; set; } = new();
    public bool StartWithWindows { get; set; } = true;
    public List<CommandFeature> Features { get; set; } = [];
}

public sealed class MqttSettings
{
    public string Platform { get; set; } = "Bemfa";
    public string Host { get; set; } = "bemfa.com";
    public int Port { get; set; } = 9501;
    public string ClientId { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public bool UseTls { get; set; }
}

public sealed class CommandFeature
{
    public string Name { get; set; } = "新功能";
    public string Topic { get; set; } = "";
    public string MatchValue { get; set; } = "on";
    public string Action { get; set; } = "shutdown";
    public string ScriptPath { get; set; } = "";
    public bool Enabled { get; set; } = true;
    [JsonIgnore] public string Icon => Action switch { "shutdown" => "⏻", "restart" => "⟳", "lock" => "▣", _ => "›_" };
    [JsonIgnore] public System.Windows.Media.Brush IconBrush => Action switch { "shutdown" => System.Windows.Media.Brushes.OrangeRed, "restart" => System.Windows.Media.Brushes.DodgerBlue, "lock" => System.Windows.Media.Brushes.SteelBlue, _ => System.Windows.Media.Brushes.MediumPurple };
    [JsonIgnore] public string ActionLabel => Action switch { "shutdown" => "执行电脑关机", "restart" => "执行电脑重启", "lock" => "锁定 Windows 会话", _ => "运行自定义脚本" };
    [JsonIgnore] public string EnabledLabel => Enabled ? "已启用" : "已关闭";
}

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly string _executableConfigPath;
    private readonly string _localConfigPath;

    public string Path { get; }

    public ConfigStore()
    {
        _executableConfigPath = System.IO.Path.Combine(AppContext.BaseDirectory, "config.json");
        _localConfigPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MQCtrl", "config.json");

        Path = CanWriteConfigLocation(_executableConfigPath)
            ? _executableConfigPath
            : _localConfigPath;
    }

    private static bool CanWriteConfigLocation(string configPath)
    {
        var directory = System.IO.Path.GetDirectoryName(configPath);
        if (string.IsNullOrWhiteSpace(directory)) return false;

        try
        {
            Directory.CreateDirectory(directory);
            if (File.Exists(configPath))
            {
                using var stream = new FileStream(configPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            }
            else
            {
                var probePath = System.IO.Path.Combine(directory, $".mqctrl-write-test-{Guid.NewGuid():N}.tmp");
                using var stream = new FileStream(probePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
            }

            return true;
        }
        catch (UnauthorizedAccessException) { return false; }
        catch (IOException) { return false; }
        catch (System.Security.SecurityException) { return false; }
    }

    private void ImportExistingConfigIfNeeded()
    {
        if (File.Exists(Path)) return;

        var sourcePath = string.Equals(Path, _executableConfigPath, StringComparison.OrdinalIgnoreCase)
            ? _localConfigPath
            : _executableConfigPath;
        if (!File.Exists(sourcePath)) return;

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        try { File.Copy(sourcePath, Path, overwrite: false); }
        catch (IOException) when (File.Exists(Path)) { }
    }

    public AppConfig Load()
    {
        ImportExistingConfigIfNeeded();

        if (!File.Exists(Path))
        {
            var config = new AppConfig
            {
                Features =
                [
                    new() { Name = "关机", Topic = "pc/shutdown", MatchValue = "on", Action = "shutdown", Enabled = false },
                    new() { Name = "重启", Topic = "pc/restart", MatchValue = "on", Action = "restart", Enabled = false },
                    new() { Name = "锁屏", Topic = "pc/lock", MatchValue = "on", Action = "lock", Enabled = false },
                    new() { Name = "自定义", Topic = "pc/custom", MatchValue = "on", Action = "script", Enabled = false }
                ]
            };
            Save(config);
            return config;
        }

        try
        {
            var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(Path), JsonOptions) ?? new AppConfig();
            var sampleTopics = new[] { "pc/shutdown", "pc/restart", "pc/backup" };
            var isLegacySample = sampleTopics.All(topic => config.Features.Any(feature => feature.Topic == topic));
            var migrated = false;
            if (isLegacySample)
            {
                var script = config.Features.First(feature => feature.Topic == "pc/backup");
                if (script.Action == "script" && script.Name == "运行脚本") { script.Name = "自定义"; script.Topic = "pc/custom"; migrated = true; }
                else if (script.Action == "script" && script.Name == "自定义" && script.Topic == "pc/backup") { script.Topic = "pc/custom"; migrated = true; }
                if (!config.Features.Any(feature => feature.Action == "lock"))
                {
                    config.Features.Insert(config.Features.IndexOf(script), new() { Name = "锁屏", Topic = "pc/lock", MatchValue = "on", Action = "lock", Enabled = false });
                    migrated = true;
                }
                else
                {
                    var lockFeature = config.Features.First(feature => feature.Action == "lock");
                    if (config.Features.IndexOf(lockFeature) > config.Features.IndexOf(script))
                    {
                        config.Features.Remove(lockFeature);
                        config.Features.Insert(config.Features.IndexOf(script), lockFeature);
                        migrated = true;
                    }
                }
            }
            if (migrated) Save(config);
            return config;
        }
        catch (JsonException ex) { throw new InvalidDataException($"配置文件格式错误：{Path}", ex); }
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var temp = Path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(config, JsonOptions));
        File.Move(temp, Path, true);
    }
}
