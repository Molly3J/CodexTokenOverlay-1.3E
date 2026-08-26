using System;

namespace CodexTokenOverlay;

internal readonly record struct OverlayRenderMetrics(double LabelFontPoints, double CompactValueFontPoints, double PanelHeaderFontPoints, double HighlightedValueFontPoints, int CapsuleRadius, int PanelRadius, int HorizontalPadding, int MetricGap, int DividerHeight, int PanelPadding, int HeaderHeight, int HighlightTopGap, int HighlightHeight, int ProgressTrackHeight, int ProgressVerticalGap, int CompactMetricGap, int EditHandleSize, int StrokeWidth)
{
	public static OverlayRenderMetrics Create(uint dpi, int scalePercent)
	{
		uint num = ((dpi == 0) ? 96u : dpi);
		int num2 = ManualAttachmentRules.SanitizeScale(scalePercent);
		double userFactor = (double)num2 / 100.0;
		double pixelFactor = (double)num / 96.0 * userFactor;
		return new OverlayRenderMetrics(ScaleFont(10.0), ScaleFont(12.0), ScaleFont(13.0), ScaleFont(15.0), Scale(10), Scale(14), Scale(10), Scale(8), Scale(14), Scale(14), Scale(22), Scale(6), Scale(44), Math.Max(1, Scale(4)), Scale(10), Scale(4), Math.Max(1, Scale(12)), Math.Max(1, Scale(1)));
		int Scale(int dip)
		{
			return (int)Math.Round((double)dip * pixelFactor, MidpointRounding.AwayFromZero);
		}
		double ScaleFont(double points)
		{
			return Math.Round(points * userFactor, 2, MidpointRounding.AwayFromZero);
		}
	}
}
