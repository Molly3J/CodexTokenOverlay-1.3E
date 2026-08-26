using System;
using System.Drawing;

namespace CodexTokenOverlay;

internal static class OverlayEditMoveDispatcher
{
	public static ManualAttachmentTransition Dispatch(ManualAttachmentCoordinator coordinator, AttachmentTargetBounds targets, OverlayEditPreviewEventArgs eventArgs, Point capsuleCenter, Func<Point, bool> hostSurfaceResolver, bool isCompletion)
	{
		ArgumentNullException.ThrowIfNull(coordinator, "coordinator");
		ArgumentNullException.ThrowIfNull(targets, "targets");
		ArgumentNullException.ThrowIfNull(eventArgs, "eventArgs");
		ArgumentNullException.ThrowIfNull(hostSurfaceResolver, "hostSurfaceResolver");
		if (eventArgs.Kind != OverlayEditGestureKind.Move)
		{
			throw new ArgumentOutOfRangeException("eventArgs");
		}
		bool hostSurfaceHit = hostSurfaceResolver(eventArgs.CursorScreen);
		if (!isCompletion)
		{
			return coordinator.PreviewMove(targets, eventArgs.CursorScreen, capsuleCenter, hostSurfaceHit);
		}
		return coordinator.CompleteMove(targets, eventArgs.CursorScreen, capsuleCenter, hostSurfaceHit);
	}
}
