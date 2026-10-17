using Velopack;
using Velopack.Sources;
using System.Reflection;

namespace MQCtrl.Services;

/// <summary>Checks public GitHub Releases and stages updates without interrupting MQTT control.</summary>
public sealed class UpdateService
{
    private readonly UpdateManager _manager = new(
        new GithubSource("https://github.com/JiessLi/MQCtrl", accessToken: null, prerelease: false));
    private readonly object _checkGate = new();
    private Task? _checkTask;
    private VelopackAsset? _pendingRelease;

    public event Action<string, string>? Log;
    public event Action<string>? StatusChanged;
    public string CurrentVersion => _manager.CurrentVersion?.ToString()
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
        ?? "未知";
    public string Status { get; private set; } = "正在检查";
    public string? AvailableVersion { get; private set; }
    public bool HasUpdate => _pendingRelease is not null || _manager.UpdatePendingRestart is not null;
    public bool IsInstalled => _manager.IsInstalled;

    public Task CheckAndDownloadAsync()
    {
        if (!_manager.IsInstalled)
        {
            SetStatus("无法检查更新");
            return Task.CompletedTask;
        }

        lock (_checkGate)
        {
            if (_checkTask is { IsCompleted: false }) return _checkTask;
            _checkTask = CheckAndDownloadCoreAsync();
            return _checkTask;
        }
    }

    private async Task CheckAndDownloadCoreAsync()
    {
        try
        {
            SetStatus("正在检查");
            var update = await _manager.CheckForUpdatesAsync();
            if (update is null)
            {
                _pendingRelease = null;
                AvailableVersion = null;
                SetStatus("无需更新");
                return;
            }

            await _manager.DownloadUpdatesAsync(update);
            _pendingRelease = update.TargetFullRelease;
            AvailableVersion = update.TargetFullRelease.Version.ToString();
            SetStatus("等待更新");
            Log?.Invoke("软件更新", "新版本已下载，将在下次启动时自动安装");
        }
        catch (Exception ex)
        {
            SetStatus("检查失败");
            Log?.Invoke("更新检查失败", ex.Message);
        }
        finally
        {
            lock (_checkGate) _checkTask = null;
        }
    }

    public void ApplyPendingUpdateAndRestart()
    {
        var release = _manager.UpdatePendingRestart ?? _pendingRelease
            ?? throw new InvalidOperationException("没有已下载的更新可供安装。");
        _manager.ApplyUpdatesAndRestart(release);
    }

    private void SetStatus(string status)
    {
        Status = status;
        StatusChanged?.Invoke(status);
    }
}
