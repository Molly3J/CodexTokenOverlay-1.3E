namespace CodexTokenOverlay;

internal sealed record ActiveThreadRouteStatus(string? ThreadId, int ActiveWindowCount, bool IsConnected, long Version, string? LastError);
