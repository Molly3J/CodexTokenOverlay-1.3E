using System;
using System.Runtime.InteropServices;

namespace CodexTokenOverlay;

internal sealed class CodexHostThemeSource : IOverlayThemeSource, IDisposable
{
	private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;

	private const int DwmwaUseImmersiveDarkMode = 20;

	private readonly object _gate = new object();

	private readonly IOverlayThemeSource _fallback;

	private readonly Func<IntPtr, (bool Success, bool IsDark)> _readHostTheme;

	private EventHandler? _changed;

	private OverlayThemeKind _current;

	private bool _hasHostTheme;

	private bool _disposed;

	public OverlayThemeKind Current
	{
		get
		{
			lock (_gate)
			{
				return _current;
			}
		}
	}

	public event EventHandler? Changed
	{
		add
		{
			lock (_gate)
			{
				if (!_disposed)
				{
					_changed = (EventHandler)Delegate.Combine(_changed, value);
				}
			}
		}
		remove
		{
			lock (_gate)
			{
				_changed = (EventHandler)Delegate.Remove(_changed, value);
			}
		}
	}

	public CodexHostThemeSource()
		: this(new WindowsOverlayThemeSource(), ReadHostTheme)
	{
	}

	internal CodexHostThemeSource(IOverlayThemeSource fallback, Func<IntPtr, (bool Success, bool IsDark)> readHostTheme)
	{
		ArgumentNullException.ThrowIfNull(fallback, "fallback");
		ArgumentNullException.ThrowIfNull(readHostTheme, "readHostTheme");
		_fallback = fallback;
		_readHostTheme = readHostTheme;
		_current = fallback.Current;
		_fallback.Changed += HandleFallbackChanged;
	}

	public void ObserveWindow(IntPtr hostWindow)
	{
		(bool Success, bool IsDark) result;
		try
		{
			result = hostWindow == IntPtr.Zero ? default : _readHostTheme(hostWindow);
		}
		catch
		{
			result = default;
		}
		OverlayThemeKind kind = result.Success ? (result.IsDark ? OverlayThemeKind.Dark : OverlayThemeKind.Light) : _fallback.Current;
		Update(kind, result.Success);
	}

	public void ClearWindow()
	{
		Update(_fallback.Current, hasHostTheme: false);
	}

	public void Dispose()
	{
		lock (_gate)
		{
			if (_disposed)
			{
				return;
			}
			_disposed = true;
			_changed = null;
		}
		_fallback.Changed -= HandleFallbackChanged;
		_fallback.Dispose();
	}

	private void HandleFallbackChanged(object? sender, EventArgs eventArgs)
	{
		lock (_gate)
		{
			if (_disposed || _hasHostTheme)
			{
				return;
			}
		}
		Update(_fallback.Current, hasHostTheme: false);
	}

	private void Update(OverlayThemeKind kind, bool hasHostTheme)
	{
		EventHandler? changed;
		lock (_gate)
		{
			if (_disposed)
			{
				return;
			}
			_hasHostTheme = hasHostTheme;
			if (_current == kind)
			{
				return;
			}
			_current = kind;
			changed = _changed;
		}
		changed?.Invoke(this, EventArgs.Empty);
	}

	private static (bool Success, bool IsDark) ReadHostTheme(IntPtr hostWindow)
	{
		if (TryReadDwmBoolean(hostWindow, DwmwaUseImmersiveDarkMode, out bool isDark) || TryReadDwmBoolean(hostWindow, DwmwaUseImmersiveDarkModeBefore20H1, out isDark))
		{
			return (true, isDark);
		}
		return (false, false);
	}

	private static bool TryReadDwmBoolean(IntPtr hostWindow, int attribute, out bool value)
	{
		int rawValue;
		int result = DwmGetWindowAttribute(hostWindow, attribute, out rawValue, sizeof(int));
		value = rawValue != 0;
		return result >= 0;
	}

	[DllImport("dwmapi.dll")]
	private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);
}
