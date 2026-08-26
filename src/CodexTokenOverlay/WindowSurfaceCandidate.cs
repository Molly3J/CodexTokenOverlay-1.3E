namespace CodexTokenOverlay;

internal sealed record WindowSurfaceCandidate(long Handle, uint ProcessId, bool IsVisible, bool IsMinimized, IntRect Bounds)
{
	public bool BoundsReadSucceeded { get; init; } = true;
}
