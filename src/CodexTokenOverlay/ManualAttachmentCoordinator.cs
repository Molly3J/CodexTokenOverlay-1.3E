using System;
using System.Drawing;

namespace CodexTokenOverlay;

internal sealed class ManualAttachmentCoordinator
{
	private readonly ManualPlacementEditState _editState = new ManualPlacementEditState();

	private bool _canSave;

	private bool _gesturePreviewActive;

	public bool IsEditing => _editState.IsActive;

	public ManualPlacementSnapshot Draft => _editState.Draft;

	public bool CanSave
	{
		get
		{
			if (IsEditing)
			{
				return _canSave;
			}
			return false;
		}
	}

	public bool ShouldApplyStaticDraft
	{
		get
		{
			if (IsEditing)
			{
				return !_gesturePreviewActive;
			}
			return false;
		}
	}

	public bool ShouldShowStaticHighlight
	{
		get
		{
			if (ShouldApplyStaticDraft)
			{
				return CanSave;
			}
			return false;
		}
	}

	public ManualAttachmentTransition BeginEdit(ManualPlacementSnapshot original, AttachmentTargetBounds targets)
	{
		ArgumentNullException.ThrowIfNull(original, "original");
		ArgumentNullException.ThrowIfNull(targets, "targets");
		_gesturePreviewActive = false;
		_editState.Begin(SanitizeSnapshot(original));
		_editState.ApplyEnabled(enabled: true);
		AttachmentTargetHit attachmentTargetHit = ResolveMainTarget(targets);
		_canSave = (object)attachmentTargetHit != null;
		return Transition(_editState.Draft, requiresPersist: false, shouldCollapse: true, attachmentTargetHit?.Bounds, ResolveCenter(_editState.Draft, targets));
	}

	public void BeginGesturePreview()
	{
		_ = Draft;
		_gesturePreviewActive = true;
	}

	public void EndGesturePreview()
	{
		_ = Draft;
		_gesturePreviewActive = false;
	}

	public ManualAttachmentTransition PreviewMove(AttachmentTargetBounds targets, Point cursor, Point capsuleCenter, bool hostSurfaceHit)
	{
		_ = Draft;
		AttachmentTargetHit attachmentTargetHit = ManualAttachmentCalculator.SelectTarget(targets, cursor, hostSurfaceHit);
		_canSave = (object)attachmentTargetHit != null;
		return Transition(Draft, requiresPersist: false, shouldCollapse: true, attachmentTargetHit?.Bounds, ((object)attachmentTargetHit == null) ? ResolveCenter(Draft, targets) : new Point?(capsuleCenter));
	}

	public ManualAttachmentTransition CompleteMove(AttachmentTargetBounds targets, Point cursor, Point capsuleCenter, bool hostSurfaceHit)
	{
		_ = Draft;
		AttachmentTargetHit attachmentTargetHit = ManualAttachmentCalculator.SelectTarget(targets, cursor, hostSurfaceHit);
		if ((object)attachmentTargetHit == null)
		{
			_canSave = false;
			return Transition(Draft, requiresPersist: false, shouldCollapse: true, null, ResolveCenter(Draft, targets));
		}
		_editState.ApplyAttachment(ManualAttachmentCalculator.Capture(attachmentTargetHit.Bounds, capsuleCenter, targets.Dpi));
		_editState.ApplyEnabled(enabled: true);
		_canSave = true;
		return Transition(Draft, requiresPersist: false, shouldCollapse: true, attachmentTargetHit.Bounds, ManualAttachmentCalculator.ResolveCenter(attachmentTargetHit.Bounds, Draft.MainAttachment, targets.Dpi));
	}

	public ManualAttachmentTransition PreviewResize(AttachmentTargetBounds targets, Point fixedTopLeft, int scalePercent, CollapsedDisplayMode display)
	{
		ManualPlacementSnapshot draft = Draft;
		AttachmentTargetHit attachmentTargetHit = ResolveMainTarget(targets);
		if ((object)attachmentTargetHit == null || targets.Dpi == 0)
		{
			_canSave = false;
			return Transition(draft, requiresPersist: false, shouldCollapse: true, null, null);
		}
		int scalePercent2 = ManualAttachmentRules.SanitizeScale(scalePercent);
		Size collapsedSize = OverlayLayoutCalculator.GetCollapsedSize(targets.Dpi, scalePercent2, display);
		checked
		{
			Point point = new Point(fixedTopLeft.X + unchecked(collapsedSize.Width / 2), fixedTopLeft.Y + unchecked(collapsedSize.Height / 2));
			_editState.ApplyScale(scalePercent2);
			_editState.ApplyAttachment(ManualAttachmentCalculator.Capture(attachmentTargetHit.Bounds, point, targets.Dpi));
			_editState.ApplyEnabled(enabled: true);
			_canSave = true;
			return Transition(Draft, requiresPersist: false, shouldCollapse: true, attachmentTargetHit.Bounds, point);
		}
	}

	public ManualAttachmentTransition Commit()
	{
		if (!CanSave)
		{
			throw new InvalidOperationException("当前手势没有有效的 Codex 吸附目标。");
		}
		ManualPlacementSnapshot snapshot = _editState.Commit()with
		{
			Enabled = true
		};
		_canSave = false;
		_gesturePreviewActive = false;
		return Transition(snapshot, requiresPersist: true, shouldCollapse: true, null, null);
	}

	public ManualAttachmentTransition Cancel()
	{
		ManualPlacementSnapshot snapshot = _editState.Cancel();
		_canSave = false;
		_gesturePreviewActive = false;
		return Transition(snapshot, requiresPersist: false, shouldCollapse: true, null, null);
	}

	public static Point? ResolveCenter(ManualPlacementSnapshot snapshot, AttachmentTargetBounds targets)
	{
		ArgumentNullException.ThrowIfNull(snapshot, "snapshot");
		ArgumentNullException.ThrowIfNull(targets, "targets");
		AttachmentTargetHit attachmentTargetHit = ResolveMainTarget(targets);
		if ((object)attachmentTargetHit == null || targets.Dpi == 0)
		{
			return null;
		}
		return ManualAttachmentCalculator.ResolveCenter(attachmentTargetHit.Bounds, snapshot.MainAttachment, targets.Dpi);
	}

	private ManualAttachmentTransition Transition(ManualPlacementSnapshot snapshot, bool requiresPersist, bool shouldCollapse, IntRect? highlightBounds, Point? resolvedCenter)
	{
		return new ManualAttachmentTransition(snapshot, IsEditing, CanSave, requiresPersist, shouldCollapse, highlightBounds, resolvedCenter);
	}

	private static ManualPlacementSnapshot SanitizeSnapshot(ManualPlacementSnapshot snapshot)
	{
		return new ManualPlacementSnapshot(snapshot.Enabled, ManualAttachmentRules.SanitizeMain(snapshot.MainAttachment), ManualAttachmentRules.SanitizeScale(snapshot.ScalePercent));
	}

	private static AttachmentTargetHit? ResolveMainTarget(AttachmentTargetBounds targets)
	{
		if (targets.MainBounds.IsEmpty)
		{
			return null;
		}
		return new AttachmentTargetHit(targets.MainHandle, targets.MainBounds);
	}
}
