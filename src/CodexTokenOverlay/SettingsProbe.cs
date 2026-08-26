using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CodexTokenOverlay;

internal static class SettingsProbe
{
	public static SettingsProbeResult Execute(SettingsProbeRequest request)
	{
		List<SettingsProbeCaseResult> list = new List<SettingsProbeCaseResult>();
		foreach (SettingsProbeCase @case in request.Cases)
		{
			OverlaySettingsLoadResult overlaySettingsLoadResult = @case.Operation switch
			{
				"Parse" => OverlaySettings.ParseJson(RequireJson(@case)), 
				"Select" => Select(@case), 
				"Load" => Load(@case), 
				"SaveReload" => SaveReload(@case), 
				_ => throw new ArgumentException("不支持的设置探针操作：" + @case.Operation), 
			};
			list.Add(new SettingsProbeCaseResult(@case.Name, overlaySettingsLoadResult.Settings, overlaySettingsLoadResult.MustPersist));
		}
		return new SettingsProbeResult(list);
	}

	private static OverlaySettingsLoadResult Select(SettingsProbeCase probeCase)
	{
		OverlaySettingsLoadResult overlaySettingsLoadResult = OverlaySettings.ParseJson(RequireJson(probeCase));
		if (!Enum.TryParse<CollapsedSlot>(probeCase.Slot, ignoreCase: true, out var result) || !probeCase.Field.HasValue)
		{
			throw new ArgumentException("Select 设置探针需要有效的 Slot 和 Field。", "probeCase");
		}
		overlaySettingsLoadResult.Settings.SelectCollapsedField(result, (DisplayField)probeCase.Field.Value);
		return overlaySettingsLoadResult;
	}

	private static OverlaySettingsLoadResult Load(SettingsProbeCase probeCase)
	{
		string text = RequireTemporaryPath(probeCase);
		if (probeCase.Json != null)
		{
			string directoryName = Path.GetDirectoryName(text);
			if (!string.IsNullOrWhiteSpace(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			File.WriteAllText(text, probeCase.Json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		}
		OverlaySettingsLoadResult overlaySettingsLoadResult = OverlaySettings.LoadFromFile(text);
		if (overlaySettingsLoadResult.MustPersist)
		{
			overlaySettingsLoadResult.Settings.Save(text);
		}
		return overlaySettingsLoadResult;
	}

	private static OverlaySettingsLoadResult SaveReload(SettingsProbeCase probeCase)
	{
		string settingsPath = RequireTemporaryPath(probeCase);
		OverlaySettings.ParseJson(RequireJson(probeCase)).Settings.Save(settingsPath);
		return new OverlaySettingsLoadResult(OverlaySettings.Load(settingsPath), MustPersist: false);
	}

	private static string RequireJson(SettingsProbeCase probeCase)
	{
		return probeCase.Json ?? throw new ArgumentException("设置探针操作需要 Json。", "probeCase");
	}

	private static string RequireTemporaryPath(SettingsProbeCase probeCase)
	{
		if (string.IsNullOrWhiteSpace(probeCase.SettingsPath) || !Path.IsPathFullyQualified(probeCase.SettingsPath))
		{
			throw new ArgumentException("设置探针需要绝对临时设置路径。", "probeCase");
		}
		string fullPath = Path.GetFullPath(probeCase.SettingsPath);
		string fullPath2 = Path.GetFullPath(Path.GetTempPath());
		if (!fullPath.StartsWith(fullPath2, StringComparison.OrdinalIgnoreCase))
		{
			throw new ArgumentException("设置探针只能使用临时设置路径。", "probeCase");
		}
		return fullPath;
	}
}
