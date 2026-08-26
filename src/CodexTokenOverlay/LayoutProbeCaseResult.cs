using System.Collections.Generic;

namespace CodexTokenOverlay;

internal sealed record LayoutProbeCaseResult(string Name, long Handle, OverlayLayoutResult Layout, IReadOnlyList<bool> ContainsClientPoints, IReadOnlyList<bool> ContainsScreenPoints);
