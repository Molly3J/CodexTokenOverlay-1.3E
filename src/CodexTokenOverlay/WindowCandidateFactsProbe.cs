using System;

namespace CodexTokenOverlay;

internal sealed class WindowCandidateFactsProbe
{
	public long Handle { get; init; }

	public uint ProcessId { get; init; }

	public bool IsCodexProcess { get; init; }

	public bool IsVisible { get; init; }

	public bool IsMinimized { get; init; }

	public long OwnerHandle { get; init; }

	public long ExtendedStyle { get; init; }

	public IntRect Bounds { get; init; }

	public string ClassName { get; init; } = string.Empty;

	public WindowCandidateFacts ToModel()
	{
		return new WindowCandidateFacts(new IntPtr(Handle), ProcessId, IsCodexProcess, IsVisible, IsMinimized, new IntPtr(OwnerHandle), ExtendedStyle, Bounds, ClassName);
	}
}
