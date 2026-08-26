using System;

namespace CodexTokenOverlay;

internal sealed class ActiveRouteThreadState
{
	private string? _observedThreadId;

	public bool ObserveAndCollapse(ActiveThreadRouteStatus routeStatus, OverlayInteractionState interaction)
	{
		ArgumentNullException.ThrowIfNull(routeStatus, "routeStatus");
		ArgumentNullException.ThrowIfNull(interaction, "interaction");
		if (string.IsNullOrWhiteSpace(routeStatus.ThreadId))
		{
			return false;
		}
		if (_observedThreadId == null)
		{
			_observedThreadId = routeStatus.ThreadId;
			return false;
		}
		if (string.Equals(_observedThreadId, routeStatus.ThreadId, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		_observedThreadId = routeStatus.ThreadId;
		return interaction.CollapseForHostChange();
	}
}
