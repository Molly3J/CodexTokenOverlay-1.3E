using System.Collections.Generic;

namespace CodexTokenOverlay;

internal sealed class SettingsProbeRequest
{
	public List<SettingsProbeCase> Cases { get; set; } = new List<SettingsProbeCase>();
}
