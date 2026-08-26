using System;
using System.Linq;

namespace CodexTokenOverlay;

internal static class InteractionProbe
{
	public static InteractionProbeResult Execute(InteractionProbeRequest request)
	{
		ArgumentNullException.ThrowIfNull(request, "request");
		return new InteractionProbeResult(request.Cases.Select(ExecuteCase).ToArray());
	}

	private static InteractionProbeCaseResult ExecuteCase(InteractionProbeCaseRequest probeCase)
	{
		OverlayInteractionState interaction = new OverlayInteractionState();
		ActiveRouteThreadState activeRouteThread = new ActiveRouteThreadState();
		OverlayAnchorTargetState anchorTarget = new OverlayAnchorTargetState();
		InteractionProbeEventResult[] events = probeCase.Events.Select(delegate(InteractionProbeEventRequest probeEvent)
		{
			bool stateChanged = probeEvent.Operation switch
			{
				"CapsuleMouseUp" => interaction.OnCapsuleMouseUp(), 
				"PointerSample" => interaction.OnPointerSample(ReadButtons(probeEvent), probeEvent.PointerInsideOverlay ?? throw new ArgumentException("PointerSample 操作需要 PointerInsideOverlay。", "probeEvent")), 
				"CollapseForHostChange" => interaction.CollapseForHostChange(), 
				"CollapseForExpandedLayoutFailure" => interaction.CollapseForExpandedLayoutFailure(), 
				"HideForSpace" => interaction.HideForSpace(), 
				"RestoreAfterSpace" => interaction.RestoreAfterSpace(), 
				"DataOnlyUpdate" => false, 
				"ObserveActiveRouteThread" => activeRouteThread.ObserveAndCollapse(ReadRouteStatus(probeEvent), interaction), 
				"ObserveAnchorTarget" => anchorTarget.ObserveAndCollapse(probeEvent.HostHandle ?? throw new ArgumentException("ObserveAnchorTarget 操作需要 HostHandle。", "probeEvent"), (AttachmentReferencePoint)probeEvent.ReferencePoint.GetValueOrDefault(), interaction), 
				_ => throw new ArgumentException("不支持的交互探针操作：" + probeEvent.Operation, "probeEvent"), 
			};
			return new InteractionProbeEventResult(interaction.State, interaction.ShouldPollOutsideClicks, interaction.IsWaitingForOpeningClickRelease, stateChanged);
		}).ToArray();
		return new InteractionProbeCaseResult(probeCase.Name, events);
	}

	private static ActiveThreadRouteStatus ReadRouteStatus(InteractionProbeEventRequest probeEvent)
	{
		if (!probeEvent.RouteActiveWindowCount.HasValue || !probeEvent.RouteIsConnected.HasValue || !probeEvent.RouteVersion.HasValue)
		{
			throw new ArgumentException("ObserveActiveRouteThread 操作需要完整 route status。", "probeEvent");
		}
		return new ActiveThreadRouteStatus(probeEvent.RouteThreadId, probeEvent.RouteActiveWindowCount.Value, probeEvent.RouteIsConnected.Value, probeEvent.RouteVersion.Value, probeEvent.RouteLastError);
	}

	private static PointerButtons ReadButtons(InteractionProbeEventRequest probeEvent)
	{
		if (!probeEvent.PressedButtons.HasValue)
		{
			throw new ArgumentException("PointerSample 操作需要 PressedButtons。", "probeEvent");
		}
		int value = probeEvent.PressedButtons.Value;
		if ((value & -8) != 0)
		{
			throw new ArgumentOutOfRangeException("probeEvent", "PointerSample 包含不支持的按键标志。");
		}
		return (PointerButtons)value;
	}
}
