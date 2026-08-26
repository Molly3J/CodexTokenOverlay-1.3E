using System.Collections.Generic;

namespace CodexTokenOverlay;

internal sealed record InteractionProbeResult(IReadOnlyList<InteractionProbeCaseResult> Cases);
