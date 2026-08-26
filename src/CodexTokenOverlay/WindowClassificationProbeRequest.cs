using System;
using System.Collections.Generic;

namespace CodexTokenOverlay;

internal sealed class WindowClassificationProbeRequest
{
	public IReadOnlyList<WindowClassificationProbeCaseRequest> Cases { get; init; } = Array.Empty<WindowClassificationProbeCaseRequest>();
}
