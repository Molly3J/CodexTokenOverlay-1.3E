using System;
using System.Collections.Generic;

namespace CodexTokenOverlay;

internal sealed class WindowClassificationProbeCaseRequest
{
	public string Name { get; init; } = string.Empty;

	public long ForegroundHandle { get; init; }

	public IReadOnlyList<WindowCandidateFactsProbe> Candidates { get; init; } = Array.Empty<WindowCandidateFactsProbe>();
}
