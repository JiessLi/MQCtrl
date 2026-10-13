# MQCtrl

MQCtrl 是一个运行在 Windows 系统托盘中的 MQTT 指令接收器，可根据主题和消息内容执行关机、重启、锁屏或自定义脚本。

## 当前首版包含

- 简洁的 Windows 原生风格界面；浅色/深色自动跟随 Windows 应用主题，无主题设置菜单。
- 标准 MQTT 3.1.1 over TCP/TLS 连接、断线自动重连、订阅启用的主题。通用模式可配置接入 Mosquitto、Home Assistant 的 MQTT 集成、EMQX、HiveMQ 等兼容平台；填写对应平台的地址、端口和鉴权信息即可。
- 巴法云与通用 MQTT 配置入口；巴法云默认 `bemfa.com:9501`，可切换 TLS `9503`。当前不支持 MQTT over WebSocket 或 MQTT 5 专属功能。
- 指令列表增删改、单条启停、精确主题匹配和忽略消息大小写的值匹配。
- 关机、重启、锁屏、自定义脚本四种默认指令；关机/重启立即执行，支持 PowerShell、BAT、CMD 脚本。
- 通知区域单击打开主界面，右键菜单含“打开界面”和“退出”；关闭窗口时最小化到托盘。
- 开机自启动开关；配置优先保存在程序同目录；目录不可写时自动回退到 `%LocalAppData%\MQCtrl\config.json`。
- 单实例限制，避免相同客户端 ID 的多个进程互相挤下线。

首次启动会生成示例指令，但默认全部关闭。填入巴法云用户私钥作为客户端 ID 后，再启用需要的指令。开机自启动使用 `-S` 参数，启动后只驻留系统托盘，不显示主窗口；也兼容 `-SILENT`，参数不区分大小写。

## 开发与发布

需要 .NET 10 SDK。在项目目录运行：

```powershell
dotnet restore
dotnet build
dotnet run
```

也可以运行 `dotnet build -c Release` 生成 Release 版本。运行机器需要安装 .NET 10 Desktop Runtime。程序启动时会检测程序目录能否写入：可写时使用程序同目录的 `config.json`，不可写时使用 `%LocalAppData%\MQCtrl\config.json`。如果所选位置没有配置，但另一个位置存在配置，程序会复制过去且保留源文件。MQTT 凭据保存在该 JSON 中，当前版本未加密，请妥善保护该文件。

## 巴法云参数

- 普通 MQTT：`bemfa.com:9501`
- TLS：`bemfa.com:9503`
- 推荐鉴权：巴法云用户私钥作为客户端 ID，用户名和密码留空。
- 在巴法云控制台创建主题；在手机端绑定相应设备后，向主题发送 `on` 等配置消息。

项目使用 MQTTnet 实现标准 MQTT 协议。新增平台时可在 `Platforms` 下实现 `IMqttPlatformAdapter`，MQTT 连接与指令执行流程保持共用。
