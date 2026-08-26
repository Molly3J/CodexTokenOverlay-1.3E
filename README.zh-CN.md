# CodexTokenOverlay 1.4.0

面向 Windows、macOS 和 Linux 的非官方 Codex Token 状态栏。程序读取本机 Codex 会话 JSONL，显示输入、输出、缓存命中、上下文占用和墙钟速率估算。

## 下载选择

| 系统 | 安装包 | 架构 | 显示方式 |
| --- | --- | --- | --- |
| Windows 10/11 | `CodexTokenOverlay-1.4.0-windows-x64-Setup.exe` | x64 | 页面内 CDP 或外部悬浮窗 |
| Windows 10/11 | `CodexTokenOverlay-1.4.0-windows-x86-Setup.exe` | x86 | 页面内 CDP 或外部悬浮窗 |
| macOS 12+ | `.dmg` 或 `.zip` | Intel x64、Apple Silicon arm64 | 置顶外部悬浮窗 |
| Linux 桌面 | `.AppImage`、`.deb`、`.rpm` 或 `.tar.gz` | x86_64 | 置顶外部悬浮窗 |

Windows 版保留原有窗口吸附和实验性页面内 CDP 状态栏。macOS/Linux 没有 Windows UI Automation、Store 包启动器和 `user32.dll`，因此使用新的跨平台外部悬浮窗；Token 解析和指标语义保持一致。

## 安装

### Windows

运行与系统架构匹配的 Setup。安装向导可选创建 `CODEX(tokenoverlay)` 快捷方式，用于同时启动 Codex 和 Overlay。

### macOS

打开 DMG，把 `CodexTokenOverlay.app` 拖入“应用程序”。当前版本采用临时签名、没有 Apple 公证；首次启动若被 Gatekeeper 拦截，请按住 Control 点击应用并选择“打开”。

页面内状态栏尚未在实体 Mac 上验证。愿意协助测试的用户可使用[macOS CDP 可行性测试提示词](docs/macos-cdp-injection-test-prompt.zh-CN.md)；该流程只做临时探针，不代表当前安装包已支持页面内模式。

### Linux

```bash
chmod +x CodexTokenOverlay-1.4.0-linux-x86_64.AppImage
./CodexTokenOverlay-1.4.0-linux-x86_64.AppImage

sudo apt install ./codex-token-overlay_1.4.0_amd64.deb
sudo dnf install ./codex-token-overlay-1.4.0.x86_64.rpm
```

默认读取 `$CODEX_HOME/sessions`；未设置 `CODEX_HOME` 时读取 `~/.codex/sessions`。可用 `--sessions /路径/sessions` 指定其他目录。

## 安全与隐私

- 程序没有分析或遥测上传功能。
- Windows 页面内模式会打开本机 Electron CDP 回环端口，同一用户会话中的其他进程可能访问该端口。
- macOS/Linux 版不启用、也不依赖 CDP。
- `RATE` 是包括推理、调度、工具调用和等待在内的墙钟估算，不是纯模型解码速度。
- 当前 Windows 包没有商业 Authenticode 签名，macOS 包没有 Apple 公证；安装前请用 `SHA256SUMS.txt` 核对哈希。

详细边界见 `PRIVACY.txt` 和 `SECURITY.md`。

## 本地构建

```powershell
pwsh -NoProfile -File .\scripts\Build-Windows.ps1 -Architecture all
pwsh -NoProfile -File .\tests\Test-Release.ps1 -Architecture x86
pwsh -NoProfile -File .\tests\Test-Release.ps1 -Architecture x64
pwsh -NoProfile -File .\tests\Test-Portable.ps1
```

推送版本标签后，GitHub Actions 会在对应原生 Runner 上生成 Windows 安装器、macOS DMG/ZIP 和 Linux AppImage/DEB/RPM/TAR。

## 非官方声明

本项目不是 OpenAI 官方产品，也不受 OpenAI 认可或维护。Codex、ChatGPT、OpenAI 名称及图标属于其各自权利人。
