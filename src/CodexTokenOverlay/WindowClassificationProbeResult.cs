using System.Collections.Generic;

namespace CodexTokenOverlay;

internal sealed record WindowClassificationProbeResult(IReadOnlyList<WindowClassificationProbeCaseResult> Cases);
