using System;

namespace CodexTokenOverlay;

internal sealed class ManualPlacementEditState
{
	private ManualPlacementSnapshot? _original;

	private ManualPlacementSnapshot? _draft;

	public bool IsActive => (object)_draft != null;

	public ManualPlacementSnapshot Draft => _draft ?? throw new InvalidOperationException("手动定位编辑尚未开始。");

	public void Begin(ManualPlacementSnapshot snapshot)
	{
		ArgumentNullException.ThrowIfNull(snapshot, "snapshot");
		if (IsActive)
		{
			throw new InvalidOperationException("手动定位编辑已经开始。");
		}
		_original = snapshot;
		_draft = snapshot with { };
	}

	public void ApplyAttachment(WindowAttachment attachment)
	{
		ArgumentNullException.ThrowIfNull(attachment, "attachment");
		_draft = Draft with
		{
			MainAttachment = attachment
		};
	}

	public void ApplyScale(int scalePercent)
	{
		_draft = Draft with
		{
			ScalePercent = ManualAttachmentRules.SanitizeScale(scalePercent)
		};
	}

	public void ApplyEnabled(bool enabled)
	{
		_draft = Draft with
		{
			Enabled = enabled
		};
	}

	public ManualPlacementSnapshot Commit()
	{
		ManualPlacementSnapshot draft = Draft;
		End();
		return draft;
	}

	public ManualPlacementSnapshot Cancel()
	{
		_ = Draft;
		ManualPlacementSnapshot? result = _original ?? throw new InvalidOperationException("手动定位编辑尚未开始。");
		End();
		return result;
	}

	private void End()
	{
		_original = null;
		_draft = null;
	}
}
