using System.Drawing;

namespace CodexTokenOverlay;

internal sealed record ManualAttachmentTransition(ManualPlacementSnapshot Draft, bool IsEditing, bool CanSave, bool RequiresPersist, bool ShouldCollapse, IntRect? HighlightBounds, Point? ResolvedCenter);
