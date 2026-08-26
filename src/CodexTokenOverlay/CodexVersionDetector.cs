using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace CodexTokenOverlay;

internal sealed record CodexVersionInfo(string? Version, string Source, string? ExecutablePath)
{
	public bool Matches(string? expectedVersion)
	{
		return !string.IsNullOrWhiteSpace(Version)
			&& !string.IsNullOrWhiteSpace(expectedVersion)
			&& string.Equals(Version, expectedVersion, StringComparison.OrdinalIgnoreCase);
	}
}

internal static class CodexVersionDetector
{
	private static readonly Regex PackageVersionPattern = new Regex(@"OpenAI\.Codex_(?<version>\d+\.\d+\.\d+\.\d+)_", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	public static CodexVersionInfo Detect()
	{
		foreach (Process process in Process.GetProcessesByName("ChatGPT"))
		{
			using (process)
			{
				try
				{
					string? path = process.MainModule?.FileName;
					string? version = ParseFromPath(path);
					if (version != null)
					{
						return new CodexVersionInfo(version, "running-process", path);
					}
				}
				catch (Exception ex) when (ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception)
				{
				}
			}
		}
		return DetectFromInstalledPackageDirectory();
	}

	internal static string? ParseFromPath(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return null;
		}
		Match match = PackageVersionPattern.Match(path);
		return match.Success ? match.Groups["version"].Value : null;
	}

	private static CodexVersionInfo DetectFromInstalledPackageDirectory()
	{
		try
		{
			string windowsApps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps");
			string[] directories = Directory.GetDirectories(windowsApps, "OpenAI.Codex_*_x64__*");
			Array.Sort(directories, StringComparer.OrdinalIgnoreCase);
			for (int i = directories.Length - 1; i >= 0; i--)
			{
				string? version = ParseFromPath(directories[i] + Path.DirectorySeparatorChar);
				if (version != null)
				{
					return new CodexVersionInfo(version, "installed-package", directories[i]);
				}
			}
		}
		catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
		{
		}
		return new CodexVersionInfo(null, "not-detected", null);
	}
}
