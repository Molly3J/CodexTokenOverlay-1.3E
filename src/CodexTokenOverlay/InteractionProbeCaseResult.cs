using System.Collections.Generic;

namespace CodexTokenOverlay;

internal sealed record InteractionProbeCaseResult(string Name, IReadOnlyList<InteractionProbeEventResult> Events);
