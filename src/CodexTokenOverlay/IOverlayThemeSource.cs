using System;

namespace CodexTokenOverlay;

internal interface IOverlayThemeSource : IDisposable
{
	OverlayThemeKind Current { get; }

	event EventHandler? Changed;
}
