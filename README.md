# MQCtrl

MQCtrl 是一个运行在 Windows 系统托盘中的 MQTT 指令接收器，可根据主题和消息内容执行关机、重启、锁屏或自定义脚本。

## 当前首版包含

- 简洁的 Windows 原生风格界面；浅色/深色自动跟随 Windows 应用主题，无主题设置菜单。
- 标准 MQTT 3.1.1 over TCP/TLS 连接、断线自动重连、订阅启用的主题。通用模式可配置接入 Mosquitto、Home Assistant 的 MQTT 集成、EMQX、HiveMQ 等兼容平台；填写对应平台的地址、端口和鉴权信息即可。
- 巴法云与通用 MQTT 配置入口；巴法云默认 `bemfa.com:9501`，可切换 TLS `9503`。当前不支持 MQTT over WebSocket 或 MQTT 5 专属功能。
- 指令列表增删改、单条启停、精确主题匹配和忽略消息大小写的值匹配。
- 关机、重启、锁屏、自定义脚本四种默认指令；关机/重启立即执行，支持 PowerShell、BAT、CMD 脚本。
- 通知区域单击打开主界面，右键菜单含“打开界面”和“退出”；关闭窗口时最小化到托盘。
- 开机自启动开关；配置固定保存在 `%LocalAppData%\MQCtrl\config.json`，避免应用更新覆盖；首次启动时会从程序同目录复制旧配置（保留原文件）。
- 通过 GitHub Releases 检查并下载更新；更新包会在下次启动时自动安装，不会为了升级强制中断 MQTT 监听。
- 单实例限制，避免相同客户端 ID 的多个进程互相挤下线。

首次启动会生成示例指令，但默认全部关闭。填入巴法云用户私钥作为客户端 ID 后，再启用需要的指令。开机自启动使用 `-S` 参数，启动后只驻留系统托盘，不显示主窗口；也兼容 `-SILENT`，参数不区分大小写。

## 开发与发布

需要 .NET 10 SDK。在项目目录运行：

```powershell
dotnet restore
dotnet build
dotnet run
```

也可以运行 `dotnet build -c Release` 生成 Release 版本。配置统一保存在 `%LocalAppData%\MQCtrl\config.json`，首次启动时会从当前程序目录迁移旧配置（复制，不删除）。MQTT 凭据保存在该 JSON 中，当前版本未加密，请妥善保护该文件。

## 安装与更新

从 [GitHub Releases](https://github.com/JiessLi/MQCtrl/releases) 下载 `MQCtrl.APP-win-Setup.exe` 安装。安装器会在缺少运行环境时引导安装 .NET 10 Desktop Runtime。程序自动检查公开 GitHub Releases 并下载更新；已下载版本在下次启动时安装，以免升级时中断正在进行的 MQTT 监听。

发布新版本时推送 `v` 开头的版本标签（例如 `v1.0.1`），GitHub Actions 会构建安装包并发布 Release。包 ID 固定为 `MQCtrl.APP`。

## 巴法云参数

- 普通 MQTT：`bemfa.com:9501`
- TLS：`bemfa.com:9503`
- 推荐鉴权：巴法云用户私钥作为客户端 ID，用户名和密码留空。
- 在巴法云控制台创建主题；在手机端绑定相应设备后，向主题发送 `on` 等配置消息。

项目使用 MQTTnet 实现标准 MQTT 协议。新增平台时可在 `Platforms` 下实现 `IMqttPlatformAdapter`，MQTT 连接与指令执行流程保持共用。
