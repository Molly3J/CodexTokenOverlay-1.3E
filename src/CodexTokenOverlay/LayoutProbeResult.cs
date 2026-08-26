using System.Collections.Generic;

namespace CodexTokenOverlay;

internal sealed record LayoutProbeResult(IReadOnlyList<LayoutProbeCaseResult> Cases);
