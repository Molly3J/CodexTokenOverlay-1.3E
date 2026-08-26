# CodexTokenOverlay 0.1.1

An unofficial token status overlay for Codex sessions on Windows, macOS, and Linux. It reads local Codex JSONL session files and displays input, output, cache, context, and wall-clock rate estimates.

中文说明：[README.zh-CN.md](README.zh-CN.md)

## Downloads

| Platform | Package | Architecture | UI mode |
| --- | --- | --- | --- |
| Windows 10/11 | `CodexTokenOverlay-0.1.1-windows-x64-Setup.exe` | x64 | In-page CDP or external overlay |
| Windows 10/11 | `CodexTokenOverlay-0.1.1-windows-x86-Setup.exe` | x86 | In-page CDP or external overlay |
| macOS 12+ | `.dmg` or `.zip` | Intel x64, Apple Silicon arm64 | External always-on-top overlay |
| Linux desktop | `.AppImage`, `.deb`, `.rpm`, or `.tar.gz` | x86_64 | External always-on-top overlay |

Windows retains the original host-window attachment and experimental in-page CDP integration. macOS and Linux use the new portable external overlay because Windows UI Automation, Store-package startup, and `user32.dll` APIs do not exist on those systems.

## Install

### Windows

Run the installer matching the operating-system architecture. The optional `CODEX(tokenoverlay)` shortcut launches Codex and the overlay together.

The experimental in-page status bar requires Codex to expose CDP on `127.0.0.1:19222`. This loopback port must be available and listening while in-page mode is active; otherwise the overlay cannot attach to the Codex page. The installed `CODEX(tokenoverlay)` shortcut configures the required `--remote-debugging-port=19222` launch flag automatically and migrates older `9222` settings to the dedicated port.

Keep CDP bound to `127.0.0.1` only. Do not create a public firewall rule or expose port `19222` to a LAN or the internet. The dedicated high port avoids collisions with common `9222`-range debugging endpoints and tools that scan them.

### macOS

Open the DMG and copy `CodexTokenOverlay.app` to Applications. The release is ad-hoc signed but not Apple-notarized; on first launch, Control-click the app and choose **Open** if Gatekeeper asks for confirmation.

In-page status-bar injection has not been verified on physical Mac hardware. Volunteers can use the Chinese [macOS CDP feasibility test prompt](docs/macos-cdp-injection-test-prompt.zh-CN.md); it runs only a temporary probe and does not mean the current package supports in-page mode.

### Linux

Use one of the following:

```bash
chmod +x CodexTokenOverlay-0.1.1-linux-x86_64.AppImage
./CodexTokenOverlay-0.1.1-linux-x86_64.AppImage

sudo apt install ./codex-token-overlay_0.1.1_amd64.deb
sudo dnf install ./codex-token-overlay-0.1.1.x86_64.rpm
```

The overlay reads `$CODEX_HOME/sessions`, or `~/.codex/sessions` when `CODEX_HOME` is not set. Pass `--sessions /path/to/sessions` to override it.

## Security and privacy

The application has no analytics or telemetry upload feature. Windows in-page mode opens the required loopback Electron CDP endpoint at `127.0.0.1:19222`; other processes in the same user session may be able to access it. macOS and Linux packages do not enable or require CDP. See `SECURITY.md` and `PRIVACY.txt`.

`RATE` is a wall-clock estimate that includes reasoning, scheduling, tool use, and waits. It is not raw decoder throughput.

All release packages are currently unsigned by a commercial Windows certificate and are not Apple-notarized. Verify downloads against `SHA256SUMS.txt`.

## Build

Windows:

```powershell
pwsh -NoProfile -File .\scripts\Build-Windows.ps1 -Architecture all
pwsh -NoProfile -File .\tests\Test-Release.ps1 -Architecture x86
pwsh -NoProfile -File .\tests\Test-Release.ps1 -Architecture x64
```

Portable parser/UI:

```powershell
pwsh -NoProfile -File .\tests\Test-Portable.ps1
```

GitHub Actions builds macOS DMG/ZIP and Linux AppImage/DEB/RPM/TAR packages on their native runners when a version tag is pushed.

## Unofficial project

This project is not an official OpenAI product and is not endorsed or maintained by OpenAI. Codex, ChatGPT, OpenAI, and their icons belong to their respective owners.
