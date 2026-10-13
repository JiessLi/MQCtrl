using System.Diagnostics;
using System.IO;
using MQCtrl.Models;

namespace MQCtrl.Services;

public static class CommandExecutor
{
    public static async Task<string> ExecuteAsync(CommandFeature feature)
    {
        ProcessStartInfo startInfo;
        switch (feature.Action)
        {
            case "shutdown":
                startInfo = new("shutdown.exe") { UseShellExecute = false, CreateNoWindow = true };
                startInfo.ArgumentList.Add("/s"); startInfo.ArgumentList.Add("/t"); startInfo.ArgumentList.Add("0");
                break;
            case "restart":
                startInfo = new("shutdown.exe") { UseShellExecute = false, CreateNoWindow = true };
                startInfo.ArgumentList.Add("/r"); startInfo.ArgumentList.Add("/t"); startInfo.ArgumentList.Add("0");
                break;
            case "lock":
                startInfo = new("rundll32.exe") { UseShellExecute = false, CreateNoWindow = true };
                startInfo.ArgumentList.Add("user32.dll,LockWorkStation");
                break;
            case "script":
                var path = feature.ScriptPath;
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return "失败：脚本文件不存在";
                if (string.Equals(Path.GetExtension(path), ".ps1", StringComparison.OrdinalIgnoreCase))
                {
                    startInfo = new("powershell.exe") { UseShellExecute = false, CreateNoWindow = true };
                    startInfo.ArgumentList.Add("-NoProfile"); startInfo.ArgumentList.Add("-ExecutionPolicy"); startInfo.ArgumentList.Add("Bypass"); startInfo.ArgumentList.Add("-File"); startInfo.ArgumentList.Add(path);
                }
                else
                {
                    startInfo = new("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(path) ?? Environment.CurrentDirectory };
                    startInfo.ArgumentList.Add("/c");
                    startInfo.ArgumentList.Add(path);
                }
                break;
            default: return "失败：未知动作类型";
        }

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null) return "失败：无法启动命令";
            if (feature.Action == "script")
            {
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(2));
                return process.ExitCode == 0 ? "执行成功" : $"脚本退出码：{process.ExitCode}";
            }
            return feature.Action switch { "shutdown" => "正在立即关机", "restart" => "正在立即重启", "lock" => "已锁定 Windows 会话", _ => "执行成功" };
        }
        catch (Exception ex) { return $"执行失败：{ex.Message}"; }
    }
}
