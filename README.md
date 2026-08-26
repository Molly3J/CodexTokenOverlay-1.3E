# CodexTokenOverlay 1.3E

An unofficial Windows token status overlay for the Codex desktop experience. It reads local Codex session JSONL files and displays input, output, cache, context, and wall-clock rate estimates. The experimental in-page backend mounts the status row below the Codex composer and safely falls back to the external overlay when unavailable.

## Install

1. Install and sign in to the latest Microsoft Store Codex/ChatGPT desktop app.
2. Run `CodexTokenOverlay-1.3E-Setup.exe`.
3. Choose whether the installer should create the desktop shortcut `CODEX(tokenoverlay)`.
4. Leave **Launch Codex + Token Overlay** selected at the end of setup.

The first launch creates a per-user in-page configuration. Codex may be restarted to enable a verified loopback CDP endpoint.

## Requirements

- Windows 10/11, x64-compatible.
- Microsoft Store package `OpenAI.Codex`.
- Windows PowerShell 5.1.
- Codex sessions under `%CODEX_HOME%\sessions` or `%USERPROFILE%\.codex\sessions`.

## Security and privacy

The overlay does not modify Store package files and has no telemetry upload feature. In-page mode opens a loopback Electron CDP endpoint that other processes in the same Windows user session may be able to access. See `SECURITY.md` and `PRIVACY.txt` before enabling it on managed or high-risk machines.

`RATE` is a wall-clock estimate that includes reasoning, scheduling, tool use, and waits. It is not raw decoder throughput.

## Build

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Release.ps1
pwsh -NoProfile -ExecutionPolicy Bypass -File .\tests\Test-Release.ps1
```

.NET SDK 10 and Inno Setup 6 are required. Build output is written to the ignored `dist` directory.

This private test build is not Authenticode-signed. Verify the installer against the private release's `SHA256SUMS.txt` before running it; Windows may show a SmartScreen origin warning.

## Unofficial project

This project is not an official OpenAI product and is not endorsed or maintained by OpenAI. Codex, ChatGPT, OpenAI, and their icons belong to their respective owners. The icon is used only to identify the local Codex launch shortcut.
