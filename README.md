# MQCtrl

想用小爱同学或米家 App 控制 Windows 电脑？MQCtrl 可以把智能家居发出的指令传到电脑：例如说一句话或运行一个米家自动化，让电脑关机、重启、锁屏，或运行你指定的脚本。

简单来说，指令会从米家/小爱同学经过巴法云（MQTT 消息服务）发送到电脑上的 MQCtrl；MQCtrl 收到你预先设置好的主题和匹配内容后，再执行对应操作。MQCtrl 是运行在 Windows 上的接收端，不负责语音识别，也不是米家 App 的替代品；使用前需要先配置好米家/小爱同学与巴法云之间的设备或自动化关联。

[下载 MQCtrl](https://github.com/JiessLi/MQCtrl/releases/latest) · [反馈问题](https://github.com/JiessLi/MQCtrl/issues) · [MIT 许可证](LICENSE)

## 软件界面

主窗口可以查看 MQTT 连接与实时日志、管理电脑控制指令，并设置是否随 Windows 启动。

![MQCtrl 主界面：管理电脑控制指令并查看实时日志](Assets/mqctrl-main-window.png)

## 主要功能

- 支持巴法云，以及其他兼容 MQTT 3.1.1 TCP/TLS 的服务
- 按主题和消息内容匹配指令，可执行关机、重启、锁屏或自定义脚本
- 支持断线自动重连、系统托盘和 Windows 开机自启动
- 界面自动跟随 Windows 浅色或深色模式
- 从 GitHub 检查并获取应用更新

## 下载与安装

前往[最新版本页面](https://github.com/JiessLi/MQCtrl/releases/latest)下载 Windows 安装包，或选择便携版。安装包会引导完成安装；便携版解压后运行 `MQCtrl.exe` 即可。

首次运行时，示例指令默认关闭。当前版本尚未进行代码签名，Windows 可能显示 SmartScreen 提示。请仅从本项目的 GitHub Releases 下载。

## 快速开始

1. 启动 MQCtrl，点击标题栏右侧的齿轮按钮打开 MQTT 设置。
2. 选择“巴法云”或“通用 MQTT”，填写对应平台提供的服务器和客户端信息并保存。使用巴法云时，按巴法云提供的说明创建并关联智能设备。
3. 在 MQCtrl 中找到要使用的指令，填写与 MQTT 平台一致的主题和匹配值，然后启用该指令。
4. 让米家 App、小爱同学或其他 MQTT 发布端向该主题发送匹配值。例如，主题为 `pc/lock`、匹配值为 `on` 时，收到 `on` 就会让电脑进入锁屏状态。

如果只想先确认电脑端是否正常，可以暂时不连接米家：在 MQTT 平台向已配置的主题发送匹配值，观察 MQCtrl 的实时日志是否显示收到消息并执行操作。

巴法云常用连接参数：`bemfa.com:9501`（TCP）或 `bemfa.com:9503`（TLS）。主题须完全匹配；消息内容会忽略首尾空格及大小写差异。MQCtrl 只订阅已启用指令使用的主题。

## 指令与安全

首次启动会创建关机、重启、锁屏和自定义四条示例指令，均为关闭状态。启用关机或重启指令后，收到匹配消息会立即执行，不设倒计时。自定义脚本以当前 Windows 用户权限运行，支持 PowerShell（`.ps1`）和批处理（`.bat`、`.cmd`）。

请谨慎启用关机、重启及脚本指令，并确保只有可信设备或账号能向相关主题发布消息。MQTT 客户端 ID、用户名和密码保存在本机配置文件中，目前未加密；请勿公开或上传该文件。程序目录可写时配置保存在程序目录下的 `config.json`；否则使用：

```text
%LocalAppData%\MQCtrl\config.json
```

MQCtrl 连接你指定的 MQTT 服务器以收发消息，并访问 GitHub 检查更新；不会把配置文件或指令内容发送给项目维护者。配置文件可能包含连接凭据，请妥善保管。

## 兼容范围

MQCtrl 支持 MQTT 3.1.1 over TCP/TLS，可连接巴法云及 Mosquitto、EMQX、HiveMQ 等提供标准 MQTT TCP/TLS 接入的服务。各平台的认证和 TLS 设置可能不同，请按平台文档填写连接参数。目前不支持 MQTT over WebSocket 或 MQTT 5 专属功能。

## 常见问题

**如何随 Windows 启动？** 首次运行默认启用“开机自启动”。登录 Windows 后，MQCtrl 会在后台运行并驻留系统托盘，不自动打开主界面；可在主窗口底部关闭此选项。

**关闭窗口后程序还在运行吗？** 是的。关闭窗口会将程序收至系统托盘并继续监听。完全退出请右键托盘图标并选择“退出”。

**如何排查连接问题？** 查看主窗口实时日志，并确认服务器地址、端口、TLS、客户端 ID、认证信息和网络访问权限正确。

## Privacy

MQCtrl 不提供云端中转服务，也不向项目维护者发送遥测、MQTT 凭据或消息内容。客户端 ID、用户名和密码用于连接你指定的 MQTT 服务；更新检查访问 GitHub Releases。配置文件保存在本机，当前未加密。更多信息见[隐私说明](PRIVACY.md)。

## Code signing policy

当前版本尚未进行代码签名，Windows 可能显示 SmartScreen 安全提示。请仅从本项目的 [GitHub Releases](https://github.com/JiessLi/MQCtrl/releases/latest) 下载。详见[代码签名政策](CODE_SIGNING_POLICY.md)。
