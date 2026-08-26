# CodexTokenOverlay 0.1.1

`0.1.1` 是本项目重新整理发布历史后的新版本基线。

## 下载选择

- Windows x64：`CodexTokenOverlay-0.1.1-windows-x64-Setup.exe`
- Windows x86：`CodexTokenOverlay-0.1.1-windows-x86-Setup.exe`
- Apple Silicon macOS：`CodexTokenOverlay-0.1.1-macos-arm64.dmg` 或 `.zip`
- Intel macOS：`CodexTokenOverlay-0.1.1-macos-x64.dmg` 或 `.zip`
- Linux AppImage：`CodexTokenOverlay-0.1.1-linux-x86_64.AppImage`
- Debian/Ubuntu：`codex-token-overlay_0.1.1_amd64.deb`
- Fedora/RHEL：`codex-token-overlay-0.1.1.x86_64.rpm`
- Linux 解压版：`CodexTokenOverlay-0.1.1-linux-x64.tar.gz`

## Windows 页面内状态栏

页面内模式要求 Codex 在 `127.0.0.1:19222` 开放并监听 CDP。安装器创建的 `CODEX(tokenoverlay)` 快捷方式会自动配置该参数，并把旧版常见 CDP 端口设置迁移到 `19222`。

该端口只能绑定本机回环地址。不要创建公网防火墙规则，也不要把 `19222` 暴露给局域网或互联网。

## 功能

- 实时显示输入、输出、缓存命中、上下文占用和墙钟 Token 速率。
- Windows 支持页面内 CDP 状态栏、窗口吸附和外部悬浮窗回退。
- macOS/Linux 使用置顶外部悬浮窗并读取相同的本机会话 JSONL。
- GitHub Release 附带 `SHA256SUMS.txt` 用于校验下载文件。

这是非官方社区项目。Windows 安装包没有商业 Authenticode 签名；macOS 应用采用临时签名、没有 Apple 公证。
