using System.Collections.Generic;

namespace CodexTokenOverlay;

internal sealed record OverlayPresentation(OverlayMetric Primary, OverlayMetric Secondary, IReadOnlyList<OverlayMetric> ExpandedRows, double ContextPercent, bool ShowContextProgress, string? StatusText);
