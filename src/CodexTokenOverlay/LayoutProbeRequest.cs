using System;
using System.Collections.Generic;

namespace CodexTokenOverlay;

internal sealed class LayoutProbeRequest
{
	public IReadOnlyList<LayoutProbeCaseRequest> Cases { get; init; } = Array.Empty<LayoutProbeCaseRequest>();
}
