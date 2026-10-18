# MQCtrl 项目交接

更新日期：2026-10-18

## 项目概况

MQCtrl 是 Windows 桌面 MQTT 控制客户端。它连接 MQTT Broker，根据主题和值匹配用户配置的指令，执行关机、重启、锁屏或自定义脚本；支持开机启动、托盘运行和 Velopack 更新。

- 仓库：<https://github.com/JiessLi/MQCtrl>
- 技术栈：WPF / .NET 10 / MQTTnet 4.3.7.1207 / Velopack 1.2.161
- 当前主分支：`main`
- 本次交接时 HEAD：`6661349e8cb0f7cf2672f31fb13b90913e374ef5`（`Release MQCtrl 1.0.4`）
- 当前最新公开版本：`v1.0.4`

## 主要代码位置

- `App.xaml.cs`：应用启动、单实例与静默启动参数处理；`-S` / `-SILENT` 不区分大小写。
- `MainWindow.xaml`、`MainWindow.xaml.cs`：主窗口、指令映射、实时日志、自动启动和版本状态。
- `ConfigDialog.xaml`、`ConfigDialog.xaml.cs`：MQTT 连接配置。
- `FeatureDialog.xaml`、`FeatureDialog.xaml.cs`：指令添加与编辑。
- `Models/AppConfig.cs`：配置模型和配置文件路径选择。
- `Services/`、`Platforms/`：业务服务与平台相关实现。
- `Assets/`：界面与程序图标资源。
- `Tools/SetExecutableIcon.ps1`：将图标写入发布的 EXE。
- `.github/workflows/release.yml`：标签触发的构建、打包和 GitHub Release 流程。
- `RELEASE_NOTES.md`：发布说明。

## 配置与运行数据

- 配置优先保存在程序目录的 `config.json`；目录不可写时回退到 `%LocalAppData%\MQCtrl\config.json`。
- 日志保存在 `%LocalAppData%\MQCtrl\logs.jsonl`，界面载入最近记录并限制日志文件大小。
- `StartWithWindows` 默认启用；读取已有配置时仍尊重用户明确保存的 `false`。
- 安装版由 Velopack 管理，包 ID 为 `MQCtrl.APP`。静默参数用于后台启动，不应弹出主窗口。
- MQTT 凭据属于敏感配置；排查或分享日志、配置时不要输出真实密钥。

## 本机开发与验证

本机使用绿色 .NET SDK，系统 `dotnet` 命令可能不在 PATH：

```powershell
& 'B:\My_Program\Dev\DotNet-SDK\dotnet.exe' build --no-restore
```

必要时对 Release 配置也执行构建。涉及 UI 的改动应实际启动调试版检查窗口布局；涉及发布流程的改动应检查 workflow 产物和版本号来源。

## 发布流程与当前状态

`.github/workflows/release.yml` 以 `v*` 标签触发。版本号从 Git 标签读取（例如 `v1.0.4`），工作流会覆盖项目文件中的版本字段；因此 `MQCtrl.csproj` 内的版本值不一定代表当前已发布版本。

工作流大致执行：发布 `win-x64`、嵌入 EXE 图标、安装固定版本的 vpk、下载 GitHub 更新源、打包 `MQCtrl.APP` 并上传 GitHub Release。发布新版本前应先更新 `RELEASE_NOTES.md`、核对版本号和工作区，再创建新版本标签；不要复用已发布的版本标签。

截至交接时，`v1.0.4` 的发布工作流运行 `39105268511` 已成功，Release 不是草稿或预发布，包含 Setup、Portable 和 Velopack 更新元数据/包。发布尚未配置代码签名，Windows SmartScreen 可能提示未识别发布者。工作流目前另有 Node.js 20 弃用提示；它不影响本次成功发布，但后续可升级相关 GitHub Actions 版本并验证。

## 近期已完成

- 日志按日期分组；日期作为独立分组标题，日志标题行右侧显示时间，详情另起一行。
- 日志启动顺序包含程序启动、配置加载、MQTT 连接和订阅信息，并区分重连场景。
- 版本状态位于页脚，以简短文字呈现；检查间隔按此前决定为 10 分钟。
- 指令图标、主窗口及相关设置/添加指令界面已按项目当前设计统一。

## 接手建议

开始修改前先检查 `git status`、当前分支与最新标签，并以代码和 GitHub 当前状态复核上述版本信息。先运行构建和相关验证；只有明确收到推送/发布要求时再提交、推送或创建标签。README 面向普通用户，不要把本文件中的内部开发说明合并进去。
