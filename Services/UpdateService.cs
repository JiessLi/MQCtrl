using Velopack;
using Velopack.Sources;

namespace MQCtrl.Services;

/// <summary>Checks public GitHub Releases and stages updates without interrupting MQTT control.</summary>
public sealed class UpdateService
{
    private readonly UpdateManager _manager = new(
        new GithubSource("https://github.com/JiessLi/MQCtrl", accessToken: null, prerelease: false));
    private int _checking;

    public event Action<string, string>? Log;

    public async Task CheckAndDownloadAsync()
    {
        if (!_manager.IsInstalled || Interlocked.Exchange(ref _checking, 1) != 0)
            return;

        try
        {
            var update = await _manager.CheckForUpdatesAsync();
            if (update is null)
                return;

            await _manager.DownloadUpdatesAsync(update);
            Log?.Invoke("软件更新", "新版本已下载，将在下次启动时自动安装");
        }
        catch (Exception ex)
        {
            Log?.Invoke("更新检查失败", ex.Message);
        }
        finally
        {
            Volatile.Write(ref _checking, 0);
        }
    }
}
