using System;
using System.IO;
using System.Text;

namespace CodexTokenOverlay;

internal static class OverlayDiagnostics
{
	private static readonly object Sync = new object();

	public static string LogPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexTokenOverlay", "overlay.log");

	public static void Write(string message)
	{
		try
		{
			string? directoryName = Path.GetDirectoryName(LogPath);
			if (!string.IsNullOrWhiteSpace(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			string value = $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}";
			lock (Sync)
			{
				File.AppendAllText(LogPath, value, Encoding.UTF8);
			}
		}
		catch
		{
		}
	}

	public static void Write(string message, Exception exception)
	{
		try
		{
			string? directoryName = Path.GetDirectoryName(LogPath);
			if (!string.IsNullOrWhiteSpace(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			string value = $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}{exception}{Environment.NewLine}";
			lock (Sync)
			{
				File.AppendAllText(LogPath, value, Encoding.UTF8);
			}
		}
		catch
		{
		}
	}
}
