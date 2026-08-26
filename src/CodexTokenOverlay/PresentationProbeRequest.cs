using System.Collections.Generic;

namespace CodexTokenOverlay;

internal sealed class PresentationProbeRequest
{
	public List<PresentationProbeCase> Cases { get; set; } = new List<PresentationProbeCase>();
}
