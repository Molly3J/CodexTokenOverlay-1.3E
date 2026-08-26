# Security policy

## Supported version

Only the latest release is supported. Cross-platform 1.4.0 packages are prerelease software until promoted after broader platform testing.

## Reporting a vulnerability

Report security issues privately to the repository owner. Do not include Codex session logs, prompts, access tokens, cookies, account identifiers, or machine-specific paths in an issue.

## CDP boundary

The experimental in-page backend starts the Microsoft Store Codex app with a loopback Chrome DevTools Protocol endpoint. The launcher selects an available port, checks that the listener belongs to an `OpenAI.Codex` package process, and verifies an `app://` page before the overlay uses it. CDP still has no application-level authentication; another process in the same Windows user session may be able to connect.

Use the stable external overlay backend if this risk is unacceptable. The project never modifies signed Microsoft Store package files.

The macOS and Linux packages use only the external overlay and do not start a CDP endpoint. They still read local Codex session logs and should run only under the intended operating-system user account.

## Package signing

Current Windows packages do not have a commercial Authenticode signature. macOS packages are ad-hoc signed but are not Apple-notarized. Verify downloaded assets against the release's `SHA256SUMS.txt` before installation.
