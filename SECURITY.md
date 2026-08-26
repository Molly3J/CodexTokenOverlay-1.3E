# Security policy

## Supported version

Only the latest release is supported. Cross-platform 0.1.1 packages are prerelease software until promoted after broader platform testing.

## Reporting a vulnerability

Report security issues privately to the repository owner. Do not include Codex session logs, prompts, access tokens, cookies, account identifiers, or machine-specific paths in an issue.

## CDP boundary

The experimental in-page backend starts the Microsoft Store Codex app with a loopback Chrome DevTools Protocol endpoint at `127.0.0.1:19222`. That port must remain available and listening while in-page mode is active. The launcher migrates older `9222` settings to the dedicated port, checks that the listener belongs to an `OpenAI.Codex` package process, and verifies an `app://` page before the overlay uses it.

CDP has no application-level authentication; another process in the same Windows user session may be able to connect. Keep it bound to loopback only. Never expose port `19222` through a public firewall rule, port forwarding, a LAN listener, or an internet-facing interface.

Use the stable external overlay backend if this risk is unacceptable. The project never modifies signed Microsoft Store package files.

The macOS and Linux packages use only the external overlay and do not start a CDP endpoint. They still read local Codex session logs and should run only under the intended operating-system user account.

## Package signing

Current Windows packages do not have a commercial Authenticode signature. macOS packages are ad-hoc signed but are not Apple-notarized. Verify downloaded assets against the release's `SHA256SUMS.txt` before installation.
