namespace CodexTokenOverlay;

internal sealed class SettingsProbeCase
{
	public string Name { get; set; } = string.Empty;

	public string Operation { get; set; } = string.Empty;

	public string? Json { get; set; }

	public string? Slot { get; set; }

	public int? Field { get; set; }

	public string? SettingsPath { get; set; }
}
