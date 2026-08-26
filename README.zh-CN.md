# CodexTokenOverlay 1.3E

面向 Windows Codex 桌面应用的非官方 Token 状态栏。它读取本机 Codex 会话 JSONL，显示输入、输出、缓存命中、上下文占用和墙钟速率估算，并可把状态栏挂载到 Codex 输入框下方。

## 一键安装

1. 确认已从 Microsoft Store 安装并登录最新版 Codex/ChatGPT 桌面应用。
2. 运行 `CodexTokenOverlay-1.3E-Setup.exe`。
3. 在安装向导中选择是否创建桌面快捷方式 `CODEX(tokenoverlay)`。
4. 保持“立即启动 Codex + Token Overlay”勾选，完成安装。

首次启动会为当前用户创建页面内状态栏配置，并在必要时重新启动 Codex 以打开本机 CDP 调试端口。若页面内挂载失败，Overlay 会回退到外部悬浮模式。

## 系统要求

- Windows 10/11 x64 或支持 x64 应用的 Windows 设备。
- Microsoft Store 包 `OpenAI.Codex`。
- PowerShell 5.1、允许当前用户运行安装目录中的本地脚本。
- Codex 会话目录：`%CODEX_HOME%\sessions`，未设置 `CODEX_HOME` 时使用 `%USERPROFILE%\.codex\sessions`。

## 快捷方式

安装向导提供可选任务：

- 名称：`CODEX(tokenoverlay)`
- 图标：Codex 图标
- 作用：启动或复用 Codex 的本机 CDP 端口，并启动 Token Overlay

开始菜单始终保留同名入口，桌面快捷方式由用户选择。

## 安全与隐私

- 本程序不会修改 Microsoft Store 中的 Codex 文件。
- 页面内模式会在回环地址打开 Electron CDP 端口。同一 Windows 用户会话中的其他本地进程可能访问该端口。
- 启动器只接受由 `OpenAI.Codex` 包进程持有的监听端口，并验证 `app://` 页面目标。
- Token 数据来自本机 Codex 会话日志；程序不提供遥测上传功能。
- 详细说明见 `PRIVACY.txt` 和 `SECURITY.md`。

## 指标语义

`RATE` 是输出 Token 增量除以墙钟时间的估算，包含推理、调度、工具调用和等待时间；它不是纯模型解码吞吐率。

## 构建

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Release.ps1
pwsh -NoProfile -ExecutionPolicy Bypass -File .\tests\Test-Release.ps1
```

需要 .NET SDK 10 和 Inno Setup 6。构建产物写入 `dist`，该目录不提交 Git。

当前私人测试版未使用商业代码签名证书。安装前请从私有 Release 的 `SHA256SUMS.txt` 核对安装包哈希；Windows 可能显示 SmartScreen 来源提示。

## 非官方声明

本项目不是 OpenAI 官方产品，也不受 OpenAI 认可或维护。Codex、ChatGPT、OpenAI 名称及图标属于其各自权利人；图标仅用于识别本机已安装的 Codex 启动入口。
