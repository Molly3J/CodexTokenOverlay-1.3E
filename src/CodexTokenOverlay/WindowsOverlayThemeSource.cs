using System;
using Microsoft.Win32;

namespace CodexTokenOverlay;

internal sealed class WindowsOverlayThemeSource : IOverlayThemeSource, IDisposable
{
	private const string PersonalizeRegistryPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize";

	private const string AppsUseLightThemeValueName = "AppsUseLightTheme";

	private readonly object _gate = new object();

	private readonly Func<object?> _readValue;

	private readonly Action<UserPreferenceChangedEventHandler> _unsubscribe;

	private readonly UserPreferenceChangedEventHandler _preferenceChangedHandler;

	private EventHandler? _changed;

	private OverlayThemeKind _current;

	private bool _subscribed;

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

	public WindowsOverlayThemeSource()
		: this(ReadRegistryValue, delegate(UserPreferenceChangedEventHandler handler)
		{
			SystemEvents.UserPreferenceChanged += handler;
		}, delegate(UserPreferenceChangedEventHandler handler)
		{
			SystemEvents.UserPreferenceChanged -= handler;
		})
	{
	}

	internal WindowsOverlayThemeSource(Func<object?> readValue, Action<UserPreferenceChangedEventHandler> subscribe, Action<UserPreferenceChangedEventHandler> unsubscribe)
	{
		ArgumentNullException.ThrowIfNull(readValue, "readValue");
		ArgumentNullException.ThrowIfNull(subscribe, "subscribe");
		ArgumentNullException.ThrowIfNull(unsubscribe, "unsubscribe");
		_readValue = readValue;
		_unsubscribe = unsubscribe;
		_preferenceChangedHandler = HandleUserPreferenceChanged;
		_current = ReadKind(_readValue);
		try
		{
			subscribe(_preferenceChangedHandler);
			_subscribed = true;
		}
		catch
		{
			_subscribed = false;
		}
	}

	public static OverlayThemeKind ResolveKind(object? value)
	{
		if (!(value is byte))
		{
			if (!(value is sbyte))
			{
				if (!(value is short))
				{
					if (!(value is ushort))
					{
						if (!(value is int))
						{
							if (!(value is uint))
							{
								if (!(value is long num))
								{
									if (value is ulong num2)
									{
										return (num2 != 0L) ? OverlayThemeKind.Light : OverlayThemeKind.Dark;
									}
									return OverlayThemeKind.Dark;
								}
								return (num != 0L) ? OverlayThemeKind.Light : OverlayThemeKind.Dark;
							}
							return ((uint)value != 0) ? OverlayThemeKind.Light : OverlayThemeKind.Dark;
						}
						return ((int)value != 0) ? OverlayThemeKind.Light : OverlayThemeKind.Dark;
					}
					return ((ushort)value != 0) ? OverlayThemeKind.Light : OverlayThemeKind.Dark;
				}
				return ((short)value != 0) ? OverlayThemeKind.Light : OverlayThemeKind.Dark;
			}
			return ((sbyte)value != 0) ? OverlayThemeKind.Light : OverlayThemeKind.Dark;
		}
		return ((byte)value != 0) ? OverlayThemeKind.Light : OverlayThemeKind.Dark;
	}

	public static OverlayThemeKind ReadKind(Func<object?> readValue)
	{
		ArgumentNullException.ThrowIfNull(readValue, "readValue");
		try
		{
			return ResolveKind(readValue());
		}
		catch
		{
			return OverlayThemeKind.Dark;
		}
	}

	internal void Refresh()
	{
		lock (_gate)
		{
			if (_disposed)
			{
				return;
			}
		}
		OverlayThemeKind overlayThemeKind = ReadKind(_readValue);
		EventHandler changed;
		lock (_gate)
		{
			if (_disposed || overlayThemeKind == _current)
			{
				return;
			}
			_current = overlayThemeKind;
			changed = _changed;
		}
		changed?.Invoke(this, EventArgs.Empty);
	}

	public void Dispose()
	{
		bool flag = false;
		lock (_gate)
		{
			if (_disposed)
			{
				return;
			}
			_disposed = true;
			flag = _subscribed;
			_subscribed = false;
			_changed = null;
		}
		if (!flag)
		{
			return;
		}
		try
		{
			_unsubscribe(_preferenceChangedHandler);
		}
		catch
		{
		}
	}

	private static object? ReadRegistryValue()
	{
		using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize");
		return registryKey?.GetValue("AppsUseLightTheme");
	}

	private void HandleUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs eventArgs)
	{
		Refresh();
	}
}
