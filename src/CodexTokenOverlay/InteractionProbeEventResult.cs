namespace CodexTokenOverlay;

internal sealed record InteractionProbeEventResult(OverlayVisualState State, bool ShouldPollOutsideClicks, bool IsWaitingForOpeningClickRelease, bool StateChanged);
