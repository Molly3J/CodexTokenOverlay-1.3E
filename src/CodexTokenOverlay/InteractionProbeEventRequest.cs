namespace CodexTokenOverlay;

internal sealed class InteractionProbeEventRequest
{
	public string Operation { get; init; } = string.Empty;

	public int? PressedButtons { get; init; }

	public bool? PointerInsideOverlay { get; init; }

	public string? RouteThreadId { get; init; }

	public int? RouteActiveWindowCount { get; init; }

	public bool? RouteIsConnected { get; init; }

	public long? RouteVersion { get; init; }

	public string? RouteLastError { get; init; }

	public long? HostHandle { get; init; }

	public int? ReferencePoint { get; init; }
}
