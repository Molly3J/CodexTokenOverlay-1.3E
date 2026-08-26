using System;

namespace CodexTokenOverlay;

internal sealed class OverlayAnchorTargetState
{
	private OverlayAnchorTargetIdentity? _observed;

	public bool ObserveAndCollapse(long hostHandle, AttachmentReferencePoint referencePoint, OverlayInteractionState interaction)
	{
		ArgumentNullException.ThrowIfNull(interaction, "interaction");
		OverlayAnchorTargetIdentity overlayAnchorTargetIdentity = new OverlayAnchorTargetIdentity(hostHandle, referencePoint);
		if ((object)_observed == null)
		{
			_observed = overlayAnchorTargetIdentity;
			return false;
		}
		if (_observed == overlayAnchorTargetIdentity)
		{
			return false;
		}
		_observed = overlayAnchorTargetIdentity;
		return interaction.CollapseForHostChange();
	}
}
