using System.Drawing;

namespace CodexTokenOverlay;

internal sealed record LayoutProbePoint(int X, int Y)
{
	public Point ToPoint()
	{
		return new Point(X, Y);
	}
}
