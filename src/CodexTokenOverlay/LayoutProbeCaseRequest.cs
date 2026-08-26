using System;
using System.Collections.Generic;

namespace CodexTokenOverlay;

internal sealed class LayoutProbeCaseRequest
{
	public string Name { get; init; } = string.Empty;

	public required LayoutProbeWindowInfo HostWindow { get; init; }

	public AnchorMode AnchorMode { get; init; }

	public bool RequestExpanded { get; init; }

	public int ExpandedRowCount { get; init; }

	public bool ShowContextProgress { get; init; }

	public LayoutProbePoint? ManualCapsuleCenter { get; init; }

	public IntRect? ComposerBounds { get; init; }

	public int ScalePercent { get; init; } = 100;

	public IReadOnlyList<LayoutProbePoint> ClientPoints { get; init; } = Array.Empty<LayoutProbePoint>();

	public IReadOnlyList<LayoutProbePoint> ScreenPoints { get; init; } = Array.Empty<LayoutProbePoint>();

	public OverlayLayoutRequest ToModel()
	{
		return new OverlayLayoutRequest(HostWindow.ToModel(), AnchorMode, RequestExpanded, ExpandedRowCount, ShowContextProgress, ManualCapsuleCenter?.ToPoint(), ScalePercent, ComposerBounds);
	}
}
