# Security policy

## Supported version

Only the latest private release is supported.

## Reporting a vulnerability

Report security issues privately to the repository owner. Do not include Codex session logs, prompts, access tokens, cookies, account identifiers, or machine-specific paths in an issue.

## CDP boundary

The experimental in-page backend starts the Microsoft Store Codex app with a loopback Chrome DevTools Protocol endpoint. The launcher selects an available port, checks that the listener belongs to an `OpenAI.Codex` package process, and verifies an `app://` page before the overlay uses it. CDP still has no application-level authentication; another process in the same Windows user session may be able to connect.

Use the stable external overlay backend if this risk is unacceptable. The project never modifies signed Microsoft Store package files.

