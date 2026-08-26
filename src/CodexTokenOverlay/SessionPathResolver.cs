using System;
using System.Collections.Generic;
using System.IO;

namespace CodexTokenOverlay;

internal static class SessionPathResolver
{
	public static string Resolve(IReadOnlyList<string>? arguments = null)
	{
		if (arguments != null)
		{
			for (int i = 0; i < arguments.Count - 1; i++)
			{
				if (arguments[i].Equals("--sessions", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(arguments[i + 1]))
				{
					return Normalize(arguments[i + 1]);
				}
			}
		}
		string text = Environment.GetEnvironmentVariable("CODEX_HOME");
		if (string.IsNullOrWhiteSpace(text))
		{
			text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
		}
		return Path.Combine(Normalize(text), "sessions");
	}

	private static string Normalize(string path)
	{
		string text = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
		if (text.Equals("~", StringComparison.Ordinal) || text.StartsWith($"~{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
		{
			text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), (text.Length == 1) ? string.Empty : text.Substring(2));
		}
		return Path.GetFullPath(text);
	}
}
