using System;
using System.Collections.Generic;

namespace CodexTokenOverlay;

internal sealed class InteractionProbeCaseRequest
{
	public string Name { get; init; } = string.Empty;

	public IReadOnlyList<InteractionProbeEventRequest> Events { get; init; } = Array.Empty<InteractionProbeEventRequest>();
}
