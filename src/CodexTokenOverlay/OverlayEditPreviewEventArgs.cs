using System.Drawing;

namespace CodexTokenOverlay;

internal sealed record OverlayEditPreviewEventArgs(OverlayEditGestureKind Kind, Point CursorScreen, Point FixedTopLeft, int ScalePercent);
