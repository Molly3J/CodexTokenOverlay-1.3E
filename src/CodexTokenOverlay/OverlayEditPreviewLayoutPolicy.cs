using System;

namespace CodexTokenOverlay;

internal static class OverlayEditPreviewLayoutPolicy
{
	public static bool ShouldApplyLayout(OverlayEditGestureKind kind, ManualAttachmentTransition transition)
	{
		ArgumentNullException.ThrowIfNull(transition, "transition");
		if (kind == OverlayEditGestureKind.Move)
		{
			return !transition.CanSave;
		}
		return true;
	}
}
