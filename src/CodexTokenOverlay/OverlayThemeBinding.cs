using System;
using System.Windows.Forms;

namespace CodexTokenOverlay;

internal sealed class OverlayThemeBinding : IDisposable
{
	private readonly object _gate = new object();

	private readonly Control _dispatcher;

	private readonly IOverlayThemeSource _source;

	private readonly Action<OverlayThemePalette> _apply;

	private readonly int _dispatcherThreadId;

	private OverlayThemeKind _desiredKind;

	private OverlayThemeKind? _lastAppliedKind;

	private bool _callbackPending;

	private bool _disposed;

	public OverlayThemeBinding(Control dispatcher, IOverlayThemeSource source, Action<OverlayThemePalette> apply)
	{
		ArgumentNullException.ThrowIfNull(dispatcher, "dispatcher");
		ArgumentNullException.ThrowIfNull(source, "source");
		ArgumentNullException.ThrowIfNull(apply, "apply");
		_dispatcher = dispatcher;
		_source = source;
		_apply = apply;
		_dispatcherThreadId = Environment.CurrentManagedThreadId;
		_desiredKind = source.Current;
		_source.Changed += HandleThemeChanged;
		RequestApply(_desiredKind);
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
			_callbackPending = false;
		}
		_source.Changed -= HandleThemeChanged;
		_source.Dispose();
	}

	private void HandleThemeChanged(object? sender, EventArgs eventArgs)
	{
		RequestApply(_source.Current);
	}

	private void RequestApply(OverlayThemeKind kind)
	{
		lock (_gate)
		{
			if (_disposed)
			{
				return;
			}
			_desiredKind = kind;
			if (_callbackPending || _lastAppliedKind == kind)
			{
				return;
			}
			_callbackPending = true;
		}
		if (!_dispatcher.InvokeRequired && Environment.CurrentManagedThreadId == _dispatcherThreadId)
		{
			ApplyPending(requireHandle: false);
			return;
		}
		if (_dispatcher.IsDisposed || _dispatcher.Disposing || !_dispatcher.IsHandleCreated)
		{
			CancelPending();
			return;
		}
		try
		{
			_dispatcher.BeginInvoke(delegate
			{
				ApplyPending(requireHandle: true);
			});
		}
		catch (ObjectDisposedException)
		{
			CancelPending();
		}
		catch (InvalidOperationException)
		{
			CancelPending();
		}
	}

	private void ApplyPending(bool requireHandle)
	{
		if (_dispatcher.IsDisposed || _dispatcher.Disposing || (requireHandle && !_dispatcher.IsHandleCreated))
		{
			CancelPending();
			return;
		}
		OverlayThemeKind desiredKind;
		lock (_gate)
		{
			if (_disposed)
			{
				_callbackPending = false;
				return;
			}
			desiredKind = _desiredKind;
			_callbackPending = false;
			if (_lastAppliedKind == desiredKind)
			{
				return;
			}
			_lastAppliedKind = desiredKind;
		}
		_apply(OverlayThemePalette.For(desiredKind));
	}

	private void CancelPending()
	{
		lock (_gate)
		{
			_callbackPending = false;
		}
	}
}
