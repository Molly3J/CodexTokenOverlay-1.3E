using System;

namespace CodexTokenOverlay;

internal sealed class LayoutProbeWindowInfo
{
	public long Handle { get; init; }

	public IntRect WindowBounds { get; init; }

	public IntRect ExtendedFrameBounds { get; init; }

	public IntRect? CaptionButtonBounds { get; init; }

	public IntRect WorkingArea { get; init; }

	public uint Dpi { get; init; }

	public WindowChromeMetrics ChromeMetrics { get; init; }

	public CodexWindowInfo ToModel()
	{
		return new CodexWindowInfo(new IntPtr(Handle), WindowBounds, ExtendedFrameBounds, CaptionButtonBounds, WorkingArea, Dpi, ChromeMetrics);
	}
}
