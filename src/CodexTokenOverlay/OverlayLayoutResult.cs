using System.Drawing;

namespace CodexTokenOverlay;

internal sealed record OverlayLayoutResult(OverlayVisualState State, CollapsedDisplayMode CollapsedDisplay, ExpansionDirection ExpansionDirection, uint Dpi, IntRect WindowBounds, IntRect CapsuleBounds, IntRect PanelBounds, int ExpandedRowHeight, int ScalePercent = 100)
{
	public bool ContainsClientPoint(Point point)
	{
		if (!CapsuleBounds.Contains(point.X, point.Y))
		{
			return PanelBounds.Contains(point.X, point.Y);
		}
		return true;
	}

	public bool ContainsScreenPoint(Point point)
	{
		return ContainsClientPoint(new Point(point.X - WindowBounds.X, point.Y - WindowBounds.Y));
	}
}
