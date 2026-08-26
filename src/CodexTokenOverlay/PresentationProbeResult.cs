using System.Collections.Generic;

namespace CodexTokenOverlay;

internal sealed record PresentationProbeResult(IReadOnlyList<PresentationProbeCaseResult> Cases);
