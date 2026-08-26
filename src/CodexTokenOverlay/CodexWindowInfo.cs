namespace CodexTokenOverlay;

internal sealed record CodexWindowInfo(nint Handle, IntRect WindowBounds, IntRect ExtendedFrameBounds, IntRect? CaptionButtonBounds, IntRect WorkingArea, uint Dpi, WindowChromeMetrics ChromeMetrics);
