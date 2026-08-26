using System;
using System.Linq;

namespace CodexTokenOverlay;

internal static class LayoutProbe
{
	public static LayoutProbeResult Execute(LayoutProbeRequest request)
	{
		ArgumentNullException.ThrowIfNull(request, "request");
		return new LayoutProbeResult(request.Cases.Select(delegate(LayoutProbeCaseRequest item)
		{
			OverlayLayoutRequest overlayLayoutRequest = item.ToModel();
			OverlayLayoutResult layout = OverlayLayoutCalculator.Calculate(overlayLayoutRequest);
			return new LayoutProbeCaseResult(item.Name, ((IntPtr)overlayLayoutRequest.HostWindow.Handle).ToInt64(), layout, item.ClientPoints.Select((LayoutProbePoint point) => layout.ContainsClientPoint(point.ToPoint())).ToArray(), item.ScreenPoints.Select((LayoutProbePoint point) => layout.ContainsScreenPoint(point.ToPoint())).ToArray());
		}).ToArray());
	}
}
