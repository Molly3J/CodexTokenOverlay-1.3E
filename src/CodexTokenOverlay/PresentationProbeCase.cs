namespace CodexTokenOverlay;

internal sealed class PresentationProbeCase
{
	public string Name { get; set; } = string.Empty;

	public string Operation { get; set; } = string.Empty;

	public TokenSnapshot? Snapshot { get; set; }

	public int? PrimaryField { get; set; }

	public int? SecondaryField { get; set; }

	public int? VisibleFields { get; set; }

	public string? StatusText { get; set; }
}
