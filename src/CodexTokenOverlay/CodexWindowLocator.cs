using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Forms;

namespace CodexTokenOverlay;

internal static class CodexWindowLocator
{
	[return: MarshalAs(UnmanagedType.Bool)]
	private delegate bool EnumWindowsCallback(nint windowHandle, nint parameter);

	private struct NativeRectangle
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	private sealed record ConfirmedCodexTargetIdentity(long HostHandle, uint ProcessId);

	private static readonly ConditionalWeakTable<CodexWindowTarget, ConfirmedCodexTargetIdentity> ConfirmedTargets = new ConditionalWeakTable<CodexWindowTarget, ConfirmedCodexTargetIdentity>();

	private const uint ProcessQueryLimitedInformation = 4096u;

	private const uint GaRoot = 2u;

	private const uint GwOwner = 4u;

	private const int GwlExStyle = -20;

	private const int DwmwaCaptionButtonBounds = 5;

	private const int DwmwaExtendedFrameBounds = 9;

	private const int SmCxSize = 30;

	private const int SmCySize = 31;

	private const int SmCxSizeFrame = 32;

	private const int SmCySizeFrame = 33;

	private const int SmCxPaddedBorder = 92;

	[DllImport("user32.dll")]
	private static extern nint GetForegroundWindow();

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetWindowText(nint windowHandle, StringBuilder text, int maximumCount);

	[DllImport("user32.dll")]
	private static extern nint GetAncestor(nint windowHandle, uint flags);

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(nint windowHandle, out uint processId);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool EnumWindows(EnumWindowsCallback callback, nint parameter);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool IsWindowVisible(nint windowHandle);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool IsWindow(nint windowHandle);

	[DllImport("user32.dll")]
	private static extern nint GetWindow(nint windowHandle, uint command);

	[DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetClassNameW")]
	private static extern int GetClassName(nint windowHandle, StringBuilder className, int maximumCount);

	[DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
	private static extern int GetWindowLong32(nint windowHandle, int index);

	[DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
	private static extern nint GetWindowLongPtr64(nint windowHandle, int index);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetWindowRect(nint windowHandle, out NativeRectangle rectangle);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool IsIconic(nint windowHandle);

	[DllImport("user32.dll")]
	private static extern uint GetDpiForWindow(nint windowHandle);

	[DllImport("user32.dll")]
	private static extern uint GetDpiForSystem();

	[DllImport("user32.dll")]
	private static extern int GetSystemMetricsForDpi(int index, uint dpi);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern nint OpenProcess(uint desiredAccess, bool inheritHandle, uint processId);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool QueryFullProcessImageName(nint processHandle, uint flags, StringBuilder executablePath, ref uint pathLength);

	[DllImport("kernel32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool CloseHandle(nint handle);

	[DllImport("dwmapi.dll")]
	private static extern int DwmGetWindowAttribute(nint windowHandle, int attribute, out NativeRectangle value, int valueSize);

	public static bool TryGetForegroundCodexTarget(out CodexWindowTarget target)
	{
		target = null;
		nint foregroundWindow = GetForegroundWindow();
		foregroundWindow = GetAncestor(foregroundWindow, 2u);
		if (foregroundWindow == IntPtr.Zero)
		{
			return false;
		}
		GetWindowThreadProcessId(foregroundWindow, out var processId);
		if (processId == 0)
		{
			return false;
		}
		if (!IsCodexDesktopProcess(processId))
		{
			return false;
		}
		if (!TryGetCodexTarget(foregroundWindow, processId, out target))
		{
			return false;
		}
		RememberConfirmedTarget(target, processId);
		return true;
	}

	public static bool TryRefreshKnownCodexTarget(CodexWindowTarget previous, out CodexWindowTarget refreshed)
	{
		ArgumentNullException.ThrowIfNull(previous, "previous");
		refreshed = null;
		nint handle = previous.HostWindow.Handle;
		if (handle == IntPtr.Zero || !ConfirmedTargets.TryGetValue(previous, out ConfirmedCodexTargetIdentity value))
		{
			return false;
		}
		GetWindowThreadProcessId(handle, out var processId);
		if (!IsKnownTargetIdentityValid(value.HostHandle, value.ProcessId, ((IntPtr)handle).ToInt64(), processId) || !IsCodexDesktopProcess(value.ProcessId))
		{
			return false;
		}
		uint processId2 = value.ProcessId;
		if (!TryEnumerateCandidates(processId2, out IReadOnlyList<WindowCandidateFacts> candidates) || !TrySelectKnownCodexTarget(handle, processId2, candidates, out CodexWindowCandidateSelection selection) || !TryReadWindowInfo(selection.Host.Handle, out CodexWindowInfo info))
		{
			return false;
		}
		refreshed = new CodexWindowTarget(info, ReadComposerBounds(selection.Host.Handle, info.WindowBounds));
		RememberConfirmedTarget(refreshed, processId2);
		return true;
	}

	public static bool TryRefreshForegroundKnownCodexTarget(CodexWindowTarget previous, out CodexWindowTarget refreshed)
	{
		ArgumentNullException.ThrowIfNull(previous, "previous");
		refreshed = null;
		nint handle = previous.HostWindow.Handle;
		nint foregroundWindow = GetAncestor(GetForegroundWindow(), GaRoot);
		if (handle == IntPtr.Zero || foregroundWindow != handle || !ConfirmedTargets.TryGetValue(previous, out ConfirmedCodexTargetIdentity value))
		{
			return false;
		}
		GetWindowThreadProcessId(handle, out var processId);
		if (!IsKnownTargetIdentityValid(value.HostHandle, value.ProcessId, ((IntPtr)handle).ToInt64(), processId) || !TryReadWindowInfo(handle, out CodexWindowInfo info))
		{
			return false;
		}
		refreshed = new CodexWindowTarget(info, ReadComposerBounds(handle, info.WindowBounds));
		RememberConfirmedTarget(refreshed, processId);
		return true;
	}

	internal static bool TrySelectKnownCodexTarget(nint previousHostHandle, uint expectedProcessId, IReadOnlyList<WindowCandidateFacts> candidates, out CodexWindowCandidateSelection selection)
	{
		ArgumentNullException.ThrowIfNull(candidates, "candidates");
		selection = null;
		if (previousHostHandle == IntPtr.Zero || expectedProcessId == 0)
		{
			return false;
		}
		CodexWindowCandidateSelection codexWindowCandidateSelection = CodexWindowClassifier.Select(candidates.Where((WindowCandidateFacts item) => item.ProcessId == expectedProcessId && item.IsCodexProcess).ToArray(), previousHostHandle);
		if ((object)codexWindowCandidateSelection == null || codexWindowCandidateSelection.Host.Handle != previousHostHandle)
		{
			return false;
		}
		selection = codexWindowCandidateSelection;
		return true;
	}

	internal static bool IsCandidateReadValid(int classNameLength, long extendedStyle, int styleReadError)
	{
		if (classNameLength > 0)
		{
			if (extendedStyle == 0L)
			{
				return styleReadError == 0;
			}
			return true;
		}
		return false;
	}

	internal static bool IsKnownTargetIdentityValid(long confirmedHostHandle, uint confirmedProcessId, long currentHostHandle, uint currentProcessId)
	{
		if (confirmedHostHandle != 0L && confirmedProcessId != 0 && confirmedHostHandle == currentHostHandle)
		{
			return confirmedProcessId == currentProcessId;
		}
		return false;
	}

	public static bool IsPointOnKnownHost(CodexWindowTarget target, System.Drawing.Point point, IReadOnlySet<long> ignoredHandles)
	{
		ArgumentNullException.ThrowIfNull(target, "target");
		ArgumentNullException.ThrowIfNull(ignoredHandles, "ignoredHandles");
		nint handle = target.HostWindow.Handle;
		if (handle == IntPtr.Zero || !ConfirmedTargets.TryGetValue(target, out ConfirmedCodexTargetIdentity value))
		{
			return false;
		}
		GetWindowThreadProcessId(handle, out var processId);
		if (!IsKnownTargetIdentityValid(value.HostHandle, value.ProcessId, ((IntPtr)handle).ToInt64(), processId))
		{
			return false;
		}
		if (!TryEnumerateWindowSurfaces(out IReadOnlyList<WindowSurfaceCandidate> candidates))
		{
			return false;
		}
		if (!IsUnderlyingWindowKnownHostForCurrentProcess(candidates, point, ignoredHandles, value.HostHandle, value.ProcessId))
		{
			return false;
		}
		nint ancestor = GetAncestor(new IntPtr(value.HostHandle), 2u);
		if (ancestor == IntPtr.Zero)
		{
			return false;
		}
		GetWindowThreadProcessId(ancestor, out var processId2);
		return IsKnownTargetIdentityValid(value.HostHandle, value.ProcessId, ((IntPtr)ancestor).ToInt64(), processId2);
	}

	internal static long? SelectUnderlyingWindowAtPoint(IReadOnlyList<WindowSurfaceCandidate> zOrderedCandidates, System.Drawing.Point point, IReadOnlySet<long> ignoredHandles, uint ignoredProcessId)
	{
		ArgumentNullException.ThrowIfNull(zOrderedCandidates, "zOrderedCandidates");
		ArgumentNullException.ThrowIfNull(ignoredHandles, "ignoredHandles");
		foreach (WindowSurfaceCandidate zOrderedCandidate in zOrderedCandidates)
		{
			if (zOrderedCandidate.Handle != 0L && !ignoredHandles.Contains(zOrderedCandidate.Handle) && (ignoredProcessId == 0 || zOrderedCandidate.ProcessId != ignoredProcessId) && zOrderedCandidate.IsVisible && !zOrderedCandidate.IsMinimized)
			{
				if (!zOrderedCandidate.BoundsReadSucceeded)
				{
					return zOrderedCandidate.Handle;
				}
				if (!zOrderedCandidate.Bounds.IsEmpty && zOrderedCandidate.Bounds.Contains(point.X, point.Y))
				{
					return zOrderedCandidate.Handle;
				}
			}
		}
		return null;
	}

	internal static bool IsUnderlyingWindowKnownHost(IReadOnlyList<WindowSurfaceCandidate> zOrderedCandidates, System.Drawing.Point point, IReadOnlySet<long> ignoredHandles, long confirmedHostHandle, uint confirmedProcessId, uint ignoredProcessId)
	{
		long? selectedHandle = SelectUnderlyingWindowAtPoint(zOrderedCandidates, point, ignoredHandles, ignoredProcessId);
		if (!selectedHandle.HasValue || confirmedHostHandle == 0L || confirmedProcessId == 0)
		{
			return false;
		}
		WindowSurfaceCandidate windowSurfaceCandidate = zOrderedCandidates.First((WindowSurfaceCandidate item) => item.Handle == selectedHandle.Value);
		if (windowSurfaceCandidate.BoundsReadSucceeded && windowSurfaceCandidate.Handle == confirmedHostHandle)
		{
			return windowSurfaceCandidate.ProcessId == confirmedProcessId;
		}
		return false;
	}

	internal static bool IsUnderlyingWindowKnownHostForCurrentProcess(IReadOnlyList<WindowSurfaceCandidate> zOrderedCandidates, System.Drawing.Point point, IReadOnlySet<long> ignoredHandles, long confirmedHostHandle, uint confirmedProcessId)
	{
		return IsUnderlyingWindowKnownHost(zOrderedCandidates, point, ignoredHandles, confirmedHostHandle, confirmedProcessId, checked((uint)Environment.ProcessId));
	}

	internal static WindowSurfaceCandidate CreateUnreadableSurfaceCandidate(long handle, uint processId, bool windowStillExists, bool isVisible, bool isMinimized)
	{
		return new WindowSurfaceCandidate(handle, processId, !windowStillExists || isVisible, windowStillExists && isMinimized, default(IntRect))
		{
			BoundsReadSucceeded = false
		};
	}

	private static void RememberConfirmedTarget(CodexWindowTarget target, uint processId)
	{
		ConfirmedTargets.Add(target, new ConfirmedCodexTargetIdentity(((IntPtr)target.HostWindow.Handle).ToInt64(), processId));
	}

	public static IntRect ConvertRelativeToScreen(IntRect windowBounds, IntRect relativeBounds)
	{
		return new IntRect(windowBounds.X + relativeBounds.X, windowBounds.Y + relativeBounds.Y, relativeBounds.Width, relativeBounds.Height);
	}

	public static object GetForegroundWindowProbe()
	{
		nint ancestor = GetAncestor(GetForegroundWindow(), 2u);
		if (ancestor == IntPtr.Zero)
		{
			return new
			{
				Found = false,
				Handle = 0L,
				ForegroundHandle = 0L,
				ProcessId = 0u,
				IsCodex = false,
				Title = string.Empty,
				HostHandle = (long?)null,
				HostWindowBounds = (IntRect?)null,
				WindowBounds = (IntRect?)null,
				ExtendedFrameBounds = (IntRect?)null,
				CaptionButtonBounds = (IntRect?)null,
				WorkingArea = (IntRect?)null,
				Dpi = (uint?)null,
				ChromeMetrics = (WindowChromeMetrics?)null
			};
		}
		GetWindowThreadProcessId(ancestor, out var processId);
		StringBuilder stringBuilder = new StringBuilder(1024);
		GetWindowText(ancestor, stringBuilder, stringBuilder.Capacity);
		bool flag = processId != 0 && IsCodexDesktopProcess(processId);
		CodexWindowTarget codexWindowTarget = null;
		if (flag && TryGetCodexTarget(ancestor, processId, out CodexWindowTarget target))
		{
			codexWindowTarget = target;
		}
		return new
		{
			Found = true,
			Handle = ((IntPtr)ancestor).ToInt64(),
			ForegroundHandle = ((IntPtr)ancestor).ToInt64(),
			ProcessId = processId,
			IsCodex = flag,
			Title = stringBuilder.ToString(),
			HostHandle = (codexWindowTarget == null) ? ((long?)null) : ((IntPtr)codexWindowTarget.HostWindow.Handle).ToInt64(),
			HostWindowBounds = codexWindowTarget?.HostWindow.WindowBounds,
			WindowBounds = codexWindowTarget?.HostWindow.WindowBounds,
			ExtendedFrameBounds = codexWindowTarget?.HostWindow.ExtendedFrameBounds,
			CaptionButtonBounds = codexWindowTarget?.HostWindow.CaptionButtonBounds,
			WorkingArea = codexWindowTarget?.HostWindow.WorkingArea,
			Dpi = codexWindowTarget?.HostWindow.Dpi,
			ComposerBounds = codexWindowTarget?.ComposerBounds,
			ChromeMetrics = codexWindowTarget?.HostWindow.ChromeMetrics
		};
	}

	private static bool TryGetCodexTarget(nint foregroundHandle, uint processId, out CodexWindowTarget target)
	{
		target = null;
		if (!TryEnumerateCandidates(processId, out IReadOnlyList<WindowCandidateFacts> candidates))
		{
			return false;
		}
		CodexWindowCandidateSelection codexWindowCandidateSelection = CodexWindowClassifier.Select(candidates, foregroundHandle);
		if ((object)codexWindowCandidateSelection == null || !TryReadWindowInfo(codexWindowCandidateSelection.Host.Handle, out CodexWindowInfo info))
		{
			return false;
		}
		target = new CodexWindowTarget(info, ReadComposerBounds(codexWindowCandidateSelection.Host.Handle, info.WindowBounds));
		return true;
	}

	private static IntRect? ReadComposerBounds(nint windowHandle, IntRect windowBounds)
	{
		try
		{
			AutomationElement automationElement = AutomationElement.FromHandle(windowHandle);
			if ((object)automationElement == null)
			{
				return null;
			}
			AutomationElementCollection automationElementCollection = automationElement.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
			for (int i = 0; i < automationElementCollection.Count; i++)
			{
				AutomationElement automationElement2 = automationElementCollection[i];
				if (automationElement2.Current.IsOffscreen || !automationElement2.Current.ClassName.StartsWith("ProseMirror", StringComparison.Ordinal))
				{
					continue;
				}
				AutomationElement automationElement3 = automationElement2;
				for (int j = 0; j < 6; j++)
				{
					if ((object)automationElement3 == null)
					{
						break;
					}
					if (automationElement3.Current.ClassName.StartsWith("_ComposerLayoutBody_", StringComparison.Ordinal))
					{
						IntRect intRect = ToIntRect(automationElement3.Current.BoundingRectangle);
						if (!IsValidComposerBounds(intRect, windowBounds))
						{
							break;
						}
						return intRect;
					}
					automationElement3 = TreeWalker.RawViewWalker.GetParent(automationElement3);
				}
			}
		}
		catch (Exception ex) when (((ex is ElementNotAvailableException || ex is InvalidOperationException || ex is COMException || ex is BadImageFormatException || ex is FileLoadException || ex is TypeLoadException) ? 1 : 0) != 0)
		{
		}
		return null;
	}

	internal static bool IsValidComposerBounds(IntRect bounds, IntRect windowBounds)
	{
		if (!bounds.IsEmpty && bounds.Width >= 320 && bounds.Height >= 40 && bounds.Left >= windowBounds.Left && bounds.Top >= windowBounds.Top && bounds.Right <= windowBounds.Right)
		{
			return bounds.Bottom <= windowBounds.Bottom;
		}
		return false;
	}

	private static bool TryEnumerateWindowSurfaces(out IReadOnlyList<WindowSurfaceCandidate> candidates)
	{
		List<WindowSurfaceCandidate> collected = new List<WindowSurfaceCandidate>();
		bool result = EnumWindows(delegate(nint windowHandle, nint _)
		{
			GetWindowThreadProcessId(windowHandle, out var processId);
			bool isVisible = IsWindowVisible(windowHandle);
			bool isMinimized = IsIconic(windowHandle);
			if (!GetWindowRect(windowHandle, out var rectangle))
			{
				collected.Add(CreateUnreadableSurfaceCandidate(((IntPtr)windowHandle).ToInt64(), processId, IsWindow(windowHandle), isVisible, isMinimized));
				return true;
			}
			collected.Add(new WindowSurfaceCandidate(((IntPtr)windowHandle).ToInt64(), processId, isVisible, isMinimized, ToIntRect(rectangle)));
			return true;
		}, IntPtr.Zero);
		candidates = collected;
		return result;
	}

	private static bool TryEnumerateCandidates(uint expectedProcessId, out IReadOnlyList<WindowCandidateFacts> candidates)
	{
		List<WindowCandidateFacts> collected = new List<WindowCandidateFacts>();
		bool result = EnumWindows(delegate(nint windowHandle, nint _)
		{
			GetWindowThreadProcessId(windowHandle, out var processId);
			if (processId != expectedProcessId)
			{
				return true;
			}
			if (TryReadCandidate(windowHandle, processId, out WindowCandidateFacts candidate))
			{
				collected.Add(candidate);
			}
			return true;
		}, IntPtr.Zero);
		candidates = collected;
		return result;
	}

	private static bool TryReadCandidate(nint windowHandle, uint processId, out WindowCandidateFacts candidate)
	{
		candidate = null;
		if (!GetWindowRect(windowHandle, out var rectangle))
		{
			return false;
		}
		StringBuilder stringBuilder = new StringBuilder(256);
		int className = GetClassName(windowHandle, stringBuilder, stringBuilder.Capacity);
		Marshal.SetLastPInvokeError(0);
		nint windowLongPtr = GetWindowLongPtr(windowHandle, -20);
		if (!IsCandidateReadValid(styleReadError: Marshal.GetLastPInvokeError(), classNameLength: className, extendedStyle: ((IntPtr)windowLongPtr).ToInt64()))
		{
			return false;
		}
		candidate = new WindowCandidateFacts(windowHandle, processId, IsCodexProcess: true, IsWindowVisible(windowHandle), IsIconic(windowHandle), GetWindow(windowHandle, 4u), ((IntPtr)windowLongPtr).ToInt64(), ToIntRect(rectangle), stringBuilder.ToString());
		return true;
	}

	private static bool TryReadWindowInfo(nint windowHandle, out CodexWindowInfo info)
	{
		info = null;
		if (!GetWindowRect(windowHandle, out var rectangle))
		{
			return false;
		}
		IntRect intRect = ToIntRect(rectangle);
		IntRect extendedFrameBounds = ((DwmGetWindowAttribute(windowHandle, 9, out var value, Marshal.SizeOf<NativeRectangle>()) == 0) ? ToIntRect(value) : intRect);
		if (extendedFrameBounds.Width < 500 || extendedFrameBounds.Height < 400)
		{
			return false;
		}
		IntRect workingArea = IntRect.FromRectangle(Screen.FromHandle(windowHandle).WorkingArea);
		uint num = ReadDpi(windowHandle);
		IntRect? captionButtonBounds = null;
		WindowChromeMetrics chromeMetrics = default(WindowChromeMetrics);
		if (num != 0)
		{
			captionButtonBounds = ReadCaptionButtonBounds(windowHandle, intRect);
			chromeMetrics = ReadChromeMetrics(num);
		}
		info = new CodexWindowInfo(windowHandle, intRect, extendedFrameBounds, captionButtonBounds, workingArea, num, chromeMetrics);
		return true;
	}

	private static IntRect? ReadCaptionButtonBounds(nint windowHandle, IntRect windowBounds)
	{
		if (DwmGetWindowAttribute(windowHandle, 5, out var value, Marshal.SizeOf<NativeRectangle>()) != 0)
		{
			return null;
		}
		IntRect relativeBounds = ToIntRect(value);
		if (relativeBounds.IsEmpty || relativeBounds.Left < 0 || relativeBounds.Top < 0 || relativeBounds.Right > windowBounds.Width || relativeBounds.Bottom > windowBounds.Height)
		{
			return null;
		}
		return ConvertRelativeToScreen(windowBounds, relativeBounds);
	}

	private static uint ReadDpi(nint windowHandle)
	{
		try
		{
			uint dpiForWindow = GetDpiForWindow(windowHandle);
			if (dpiForWindow != 0)
			{
				return dpiForWindow;
			}
		}
		catch (EntryPointNotFoundException)
		{
		}
		try
		{
			return GetDpiForSystem();
		}
		catch (EntryPointNotFoundException)
		{
			return 0u;
		}
	}

	private static WindowChromeMetrics ReadChromeMetrics(uint dpi)
	{
		try
		{
			return new WindowChromeMetrics(GetSystemMetricsForDpi(30, dpi), GetSystemMetricsForDpi(31, dpi), GetSystemMetricsForDpi(32, dpi), GetSystemMetricsForDpi(33, dpi), GetSystemMetricsForDpi(92, dpi));
		}
		catch (EntryPointNotFoundException)
		{
			return default(WindowChromeMetrics);
		}
	}

	private static IntRect ToIntRect(NativeRectangle rectangle)
	{
		return new IntRect(rectangle.Left, rectangle.Top, rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top);
	}

	private static IntRect ToIntRect(Rect rectangle)
	{
		return new IntRect((int)Math.Round(rectangle.Left, MidpointRounding.AwayFromZero), (int)Math.Round(rectangle.Top, MidpointRounding.AwayFromZero), (int)Math.Round(rectangle.Width, MidpointRounding.AwayFromZero), (int)Math.Round(rectangle.Height, MidpointRounding.AwayFromZero));
	}

	private static nint GetWindowLongPtr(nint windowHandle, int index)
	{
		if (IntPtr.Size != 8)
		{
			return GetWindowLong32(windowHandle, index);
		}
		return GetWindowLongPtr64(windowHandle, index);
	}

	private static bool IsCodexDesktopProcess(uint processId)
	{
		nint num = OpenProcess(4096u, inheritHandle: false, processId);
		if (num == IntPtr.Zero)
		{
			return false;
		}
		try
		{
			uint pathLength = 2048u;
			StringBuilder stringBuilder = new StringBuilder((int)pathLength);
			if (!QueryFullProcessImageName(num, 0u, stringBuilder, ref pathLength))
			{
				return false;
			}
			string text = stringBuilder.ToString();
			string fileName = Path.GetFileName(text);
			if (!fileName.Equals("ChatGPT.exe", StringComparison.OrdinalIgnoreCase) && !fileName.Equals("Codex.exe", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
			if (text.Contains("\\WindowsApps\\OpenAI.Codex_", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			if (fileName.Equals("Codex.exe", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			return false;
		}
		finally
		{
			CloseHandle(num);
		}
	}
}
