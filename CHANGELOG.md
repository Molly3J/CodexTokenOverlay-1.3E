# Changelog

## Unreleased

- Moved the Windows in-page CDP default from the commonly scanned `9222` range to dedicated loopback port `19222`.
- Added automatic migration of older persisted CDP settings and release checks that lock the launcher and overlay defaults together.
- Documented that `127.0.0.1:19222` must be available for in-page mode and must never be exposed to a LAN or the internet.

## 1.4.0 - 2026-08-26

- Added a cross-platform Avalonia external overlay for macOS and Linux.
- Added Windows x86 and x64 architecture-aware installers.
- Added macOS Intel and Apple Silicon DMG/ZIP packages.
- Added Linux x86_64 AppImage, DEB, RPM, and TAR packages.
- Added native-runner parser probes and tag-driven GitHub Release automation.
- Preserved the Windows-only CDP and host-window integration with documented platform boundaries.

## 1.3E - 2026-08-26

- Added first-run, one-click configuration for the experimental in-page status row.
- Added verified dynamic loopback CDP port selection with external-overlay fallback.
- Added an installer choice for the `CODEX(tokenoverlay)` desktop shortcut.
- Added the Codex icon to the desktop and Start menu launch shortcuts.
- Replaced machine-specific assembly metadata with neutral release metadata.
- Added deterministic, no-PDB release settings and source/release privacy scans.
- Added privacy, security, license, and unofficial-project notices.
