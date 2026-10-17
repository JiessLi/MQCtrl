# MQCtrl 代码签名政策

## 当前状态

MQCtrl 当前版本未进行代码签名，SignPath Foundation 申请尚未提交。申请获批并完成服务配置之前，不会声称软件已签名，也不会把未签名文件标记为已签名。

申请获批后，计划使用 [SignPath.io](https://about.signpath.io/) 免费代码签名服务，证书由 [SignPath Foundation](https://signpath.org/) 提供（“Free code signing provided by SignPath.io, certificate by SignPath Foundation.”）。服务仅在申请获批并完成配置后启用。

## 签名责任

SignPath 已确认单人维护项目可由同一维护者承担以下角色。MQCtrl 当前由项目维护者 [JiessLi](https://github.com/JiessLi) 兼任：

- **Authors/Committers：**维护 MQCtrl 源代码和项目配置。
- **Reviewers：**审查代码变更及 Pull Request。
- **Approvers：**在每次正式发布签名前独立检查待签名文件和版本信息，并人工批准或拒绝签名请求。

每次正式发布签名请求都需要人工审批；签名只用于经审批的 MQCtrl 发布文件。SignPath 建议由独立 CI 账号通过 GitHub Actions 提交签名请求，由 JiessLi 作为 Approver 审批。若角色安排或签名服务发生变化，本政策将同步更新。

## 账户保护

所有可访问项目源代码、发布流程或签名服务的人员，均应为其 GitHub 和 SignPath 账户启用多重身份验证（MFA），并妥善保护访问凭据。

## 隐私

代码签名不会改变 MQCtrl 的数据处理方式。请参阅 [MQCtrl 隐私说明](PRIVACY.md)。

## 联系方式

如对本政策有疑问，请通过 [GitHub Issues](https://github.com/JiessLi/MQCtrl/issues) 联系项目维护者。
