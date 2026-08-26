# CodexTokenOverlay 1.3E

Private experimental Windows release.

## Highlights

- One-click first-run configuration for the in-page Token status row.
- Installer choice for the `CODEX(tokenoverlay)` desktop shortcut.
- Codex icon on desktop and Start menu launchers.
- Verified loopback CDP listener ownership and compatible `app://` page detection.
- Dynamic port selection in the 9222-9232 range.
- Stable external-overlay fallback when in-page mounting is unavailable.
- Sanitized deterministic build with no PDB or build-machine path.

## Verified environment

- Windows Codex Store package `OpenAI.Codex 26.820.7780.0`.
- Fresh settings creation, repeated-launch idempotence, optional desktop task, token parser fixture, real session token probe, and live DOM attachment.

## Important

- This is an unofficial, private experimental release.
- The binaries are not Authenticode-signed. Verify `SHA256SUMS.txt` before installation.
- In-page mode opens a loopback Electron CDP endpoint that other processes in the same Windows user session may be able to access.
- `RATE` is a wall-clock estimate, not raw model decoder throughput.

