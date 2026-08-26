using System;
using System.Collections.Generic;
using System.Linq;

namespace CodexTokenOverlay;

internal static class CodexWindowClassifier
{
	internal const long WsExToolWindow = 128L;

	internal const long WsExLayered = 524288L;

	private const string ChromiumTopLevelClass = "Chrome_WidgetWin_1";

	public static CodexWindowCandidateSelection? Select(IReadOnlyList<WindowCandidateFacts> candidates, nint foregroundHandle)
	{
		ArgumentNullException.ThrowIfNull(candidates, "candidates");
		WindowCandidateFacts foreground = candidates.FirstOrDefault((WindowCandidateFacts item) => item.Handle == foregroundHandle);
		if ((object)foreground == null || !foreground.IsCodexProcess || !foreground.IsVisible)
		{
			return null;
		}
		WindowCandidateFacts windowCandidateFacts = candidates.Where((WindowCandidateFacts item) => item.ProcessId == foreground.ProcessId && item.IsCodexProcess).ToArray().Where(IsHost)
			.OrderByDescending(Area)
			.ThenBy((WindowCandidateFacts item) => ((IntPtr)item.Handle).ToInt64())
			.FirstOrDefault();
		if ((object)windowCandidateFacts == null)
		{
			return null;
		}
		return new CodexWindowCandidateSelection(windowCandidateFacts);
	}

	private static bool IsHost(WindowCandidateFacts item)
	{
		if (item.IsVisible && !item.IsMinimized && item.OwnerHandle == IntPtr.Zero && item.ClassName.Equals("Chrome_WidgetWin_1", StringComparison.Ordinal) && item.Bounds.Width >= 500 && item.Bounds.Height >= 400)
		{
			return (item.ExtendedStyle & 0x80080) == 0;
		}
		return false;
	}

	private static long Area(WindowCandidateFacts item)
	{
		return (long)item.Bounds.Width * (long)item.Bounds.Height;
	}
}
