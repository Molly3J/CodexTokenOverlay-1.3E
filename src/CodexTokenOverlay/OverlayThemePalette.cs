using System.Drawing;

namespace CodexTokenOverlay;

internal sealed record OverlayThemePalette(Color Background, Color Label, Color Value, Color Accent, Color Border, Color Divider, Color ProgressTrack, Color ProgressStart, Color ProgressEnd, Color TargetHighlight)
{
	private static readonly OverlayThemePalette DarkPalette = new OverlayThemePalette(Color.FromArgb(36, 38, 45), Color.FromArgb(157, 161, 170), Color.FromArgb(245, 245, 247), Color.FromArgb(185, 174, 255), Color.FromArgb(36, 255, 255, 255), Color.FromArgb(80, 84, 93), Color.FromArgb(70, 74, 83), Color.FromArgb(142, 126, 255), Color.FromArgb(181, 169, 255), Color.FromArgb(142, 126, 255));

	private static readonly OverlayThemePalette LightPalette = new OverlayThemePalette(Color.FromArgb(244, 244, 246), Color.FromArgb(92, 96, 105), Color.FromArgb(28, 29, 33), Color.FromArgb(91, 72, 190), Color.FromArgb(32, 0, 0, 0), Color.FromArgb(208, 210, 216), Color.FromArgb(221, 222, 227), Color.FromArgb(111, 91, 218), Color.FromArgb(150, 132, 232), Color.FromArgb(111, 91, 218));

	public static OverlayThemePalette For(OverlayThemeKind kind)
	{
		if (kind != OverlayThemeKind.Light)
		{
			return DarkPalette;
		}
		return LightPalette;
	}
}
