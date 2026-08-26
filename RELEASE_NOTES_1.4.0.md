# CodexTokenOverlay 1.4.0 多平台预发布

本版本新增真正的跨平台外部悬浮窗，并补齐参考图所示的多系统安装资产。

## 下载安装包

- Windows x64：`CodexTokenOverlay-1.4.0-windows-x64-Setup.exe`
- Windows x86：`CodexTokenOverlay-1.4.0-windows-x86-Setup.exe`
- Apple Silicon macOS：`CodexTokenOverlay-1.4.0-macos-arm64.dmg` 或 `.zip`
- Intel macOS：`CodexTokenOverlay-1.4.0-macos-x64.dmg` 或 `.zip`
- Linux 通用便携版：`CodexTokenOverlay-1.4.0-linux-x86_64.AppImage`
- Debian/Ubuntu：`codex-token-overlay_1.4.0_amd64.deb`
- Fedora/RHEL：`codex-token-overlay-1.4.0.x86_64.rpm`
- Linux 解压版：`CodexTokenOverlay-1.4.0-linux-x64.tar.gz`

## 功能边界

- Windows x86/x64：保留页面内 CDP、外部悬浮窗、窗口吸附和一键 Codex 启动器。
- macOS/Linux：使用置顶外部悬浮窗，共用相同 JSONL Token 解析；不包含 Windows 专属的窗口吸附、Store 包启动和 CDP 自动配置。
- 默认读取 `$CODEX_HOME/sessions` 或用户目录下的 `.codex/sessions`。

## 安全提示

当前 Windows 安装包没有商业 Authenticode 签名；macOS 应用只有临时签名，没有 Apple 公证。请先用 `SHA256SUMS.txt` 核对下载文件。Windows 页面内模式的本机 CDP 风险见 `SECURITY.md`。

这是首个多平台版本，因此先标记为 prerelease；自动化构建会在 Windows、macOS、Linux 原生 Runner 上分别完成编译、固定样本解析测试和打包。
