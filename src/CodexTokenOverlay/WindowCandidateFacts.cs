namespace CodexTokenOverlay;

internal sealed record WindowCandidateFacts(nint Handle, uint ProcessId, bool IsCodexProcess, bool IsVisible, bool IsMinimized, nint OwnerHandle, long ExtendedStyle, IntRect Bounds, string ClassName);
