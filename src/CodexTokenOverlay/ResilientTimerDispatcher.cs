using System;

namespace CodexTokenOverlay;

internal static class ResilientTimerDispatcher
{
	public static bool Run(Action action, Action<Exception> recover)
	{
		ArgumentNullException.ThrowIfNull(action);
		ArgumentNullException.ThrowIfNull(recover);
		try
		{
			action();
			return true;
		}
		catch (Exception exception) when (!IsFatal(exception))
		{
			try
			{
				recover(exception);
			}
			catch (Exception recoveryException) when (!IsFatal(recoveryException))
			{
				OverlayDiagnostics.Write("timer recovery failed", new AggregateException(exception, recoveryException));
			}
			return false;
		}
	}

	private static bool IsFatal(Exception exception)
	{
		return exception is OutOfMemoryException or StackOverflowException or AccessViolationException;
	}
}
