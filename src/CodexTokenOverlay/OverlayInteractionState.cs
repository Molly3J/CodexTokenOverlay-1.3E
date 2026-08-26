namespace CodexTokenOverlay;

internal sealed class OverlayInteractionState
{
	private PointerButtons _previousButtons;

	public OverlayVisualState State { get; private set; }

	public bool ShouldPollOutsideClicks => State == OverlayVisualState.Expanded;

	public bool IsWaitingForOpeningClickRelease { get; private set; }

	public bool OnCapsuleMouseUp()
	{
		if (State == OverlayVisualState.HiddenForSpace)
		{
			return false;
		}
		_previousButtons = PointerButtons.None;
		if (State == OverlayVisualState.Expanded)
		{
			State = OverlayVisualState.Collapsed;
			IsWaitingForOpeningClickRelease = false;
			return true;
		}
		State = OverlayVisualState.Expanded;
		IsWaitingForOpeningClickRelease = true;
		return true;
	}

	public bool OnPointerSample(PointerButtons pressedButtons, bool pointerInsideOverlay)
	{
		if (State != OverlayVisualState.Expanded)
		{
			return false;
		}
		if (IsWaitingForOpeningClickRelease)
		{
			_previousButtons = pressedButtons;
			if (pressedButtons == PointerButtons.None)
			{
				IsWaitingForOpeningClickRelease = false;
			}
			return false;
		}
		PointerButtons num = pressedButtons & ~_previousButtons;
		_previousButtons = pressedButtons;
		if (num != PointerButtons.None && !pointerInsideOverlay)
		{
			State = OverlayVisualState.Collapsed;
			return true;
		}
		return false;
	}

	public bool CollapseForHostChange()
	{
		return Collapse();
	}

	public bool CollapseForExpandedLayoutFailure()
	{
		return Collapse();
	}

	public bool HideForSpace()
	{
		if (State == OverlayVisualState.HiddenForSpace)
		{
			return false;
		}
		State = OverlayVisualState.HiddenForSpace;
		_previousButtons = PointerButtons.None;
		IsWaitingForOpeningClickRelease = false;
		return true;
	}

	public bool RestoreAfterSpace()
	{
		if (State != OverlayVisualState.HiddenForSpace)
		{
			return false;
		}
		State = OverlayVisualState.Collapsed;
		return true;
	}

	private bool Collapse()
	{
		if (State == OverlayVisualState.Collapsed)
		{
			return false;
		}
		State = OverlayVisualState.Collapsed;
		_previousButtons = PointerButtons.None;
		IsWaitingForOpeningClickRelease = false;
		return true;
	}
}
