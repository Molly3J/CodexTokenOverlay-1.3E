using System;
using System.Collections.Generic;

namespace CodexTokenOverlay;

internal sealed class InteractionProbeRequest
{
	public IReadOnlyList<InteractionProbeCaseRequest> Cases { get; init; } = Array.Empty<InteractionProbeCaseRequest>();
}
