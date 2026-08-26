using System.Drawing;

namespace CodexTokenOverlay;

internal sealed record OverlayLayoutRequest(CodexWindowInfo HostWindow, AnchorMode AnchorMode, bool RequestExpanded, int ExpandedRowCount, bool ShowContextProgress, Point? ManualCapsuleCenter = null, int ScalePercent = 100, IntRect? ComposerBounds = null);
