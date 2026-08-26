namespace CodexTokenOverlay;

internal readonly record struct OverlayRenderDecorationState(bool ShowBorder, bool ShowDragHint, bool ShowResizeHandle, string DragHintText, IntRect DragHintBounds, double DragHintFontPoints);
