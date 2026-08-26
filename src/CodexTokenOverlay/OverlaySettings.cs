using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace CodexTokenOverlay;

internal sealed class OverlaySettings
{
	private sealed class PersistedSettings
	{
		public int? SettingsVersion { get; set; }

		public int? AnchorMode { get; set; }

		public int? VisibleFields { get; set; }

		public int? CollapsedPrimaryField { get; set; }

		public int? CollapsedSecondaryField { get; set; }

		public bool? ManualPlacementEnabled { get; set; }

		public PersistedWindowAttachment? MainAttachment { get; set; }

		public int? OverlayScalePercent { get; set; }

		public int? DisplayBackend { get; set; }

		public int? CdpPort { get; set; }

		public string? CdpExpectedCodexVersion { get; set; }
	}

	private sealed class PersistedWindowAttachment
	{
		public int? ReferencePoint { get; set; }

		public double? OffsetXDip { get; set; }

		public double? OffsetYDip { get; set; }
	}

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true,
		PropertyNameCaseInsensitive = true
	};

	private static readonly string DefaultSettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexTokenOverlay", "settings.json");

	private const DisplayField DefaultVisibleFields = DisplayField.Total | DisplayField.Input | DisplayField.Output | DisplayField.CacheHit | DisplayField.CacheMiss | DisplayField.Context | DisplayField.ContextPercent | DisplayField.CacheHitRate;

	public const int CurrentSettingsVersion = 1;

	public int SettingsVersion { get; private set; }

	public AnchorMode AnchorMode { get; set; }

	public DisplayField VisibleFields { get; set; }

	public DisplayField CollapsedPrimaryField { get; private set; }

	public DisplayField CollapsedSecondaryField { get; private set; }

	public bool ManualPlacementEnabled { get; set; }

	public WindowAttachment MainAttachment { get; set; } = ManualAttachmentRules.DefaultMainAttachment;

	public int OverlayScalePercent { get; set; }

	public DisplayBackendKind DisplayBackend { get; set; }

	public int CdpPort { get; set; }

	public string? CdpExpectedCodexVersion { get; set; }

	public static OverlaySettings CreateDefault()
	{
		return new OverlaySettings
		{
			SettingsVersion = 1,
			AnchorMode = AnchorMode.ComposerBottomStrip,
			VisibleFields = (DisplayField.Total | DisplayField.Input | DisplayField.Output | DisplayField.CacheHit | DisplayField.CacheMiss | DisplayField.Context | DisplayField.ContextPercent | DisplayField.CacheHitRate),
			CollapsedPrimaryField = DisplayField.Total,
			CollapsedSecondaryField = DisplayField.ContextPercent,
			ManualPlacementEnabled = false,
			MainAttachment = ManualAttachmentRules.DefaultMainAttachment,
			OverlayScalePercent = 100,
			DisplayBackend = DisplayBackendKind.ExperimentalCdp,
			CdpPort = 9222,
			CdpExpectedCodexVersion = null
		};
	}

	public static OverlaySettings Load(string? settingsPath = null)
	{
		string text = settingsPath ?? DefaultSettingsPath;
		OverlaySettingsLoadResult overlaySettingsLoadResult = LoadFromFile(text);
		if (overlaySettingsLoadResult.MustPersist)
		{
			overlaySettingsLoadResult.Settings.Save(text);
		}
		return overlaySettingsLoadResult.Settings;
	}

	internal static OverlaySettingsLoadResult LoadFromFile(string path)
	{
		try
		{
			if (!File.Exists(path))
			{
				return new OverlaySettingsLoadResult(CreateDefault(), MustPersist: false);
			}
			return ParseJson(File.ReadAllText(path, Encoding.UTF8));
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			return new OverlaySettingsLoadResult(CreateDefault(), MustPersist: false);
		}
	}

	internal static OverlaySettingsLoadResult ParseJson(string json)
	{
		try
		{
			PersistedSettings persistedSettings = JsonSerializer.Deserialize<PersistedSettings>(json, JsonOptions);
			if (persistedSettings == null)
			{
				return new OverlaySettingsLoadResult(CreateDefault(), MustPersist: false);
			}
			if (!persistedSettings.SettingsVersion.HasValue)
			{
				OverlaySettings overlaySettings = CreateDefault();
				overlaySettings.AnchorMode = AnchorMode.TitleBarTopRight;
				overlaySettings.VisibleFields = DisplayFieldRules.SanitizeVisible((DisplayField)(persistedSettings.VisibleFields ?? 639));
				overlaySettings.ManualPlacementEnabled = persistedSettings.ManualPlacementEnabled == true;
				return new OverlaySettingsLoadResult(overlaySettings, MustPersist: true);
			}
			OverlaySettings overlaySettings2 = CreateDefault();
			overlaySettings2.AnchorMode = SanitizeAnchorMode(persistedSettings.AnchorMode);
			overlaySettings2.VisibleFields = DisplayFieldRules.SanitizeVisible((DisplayField)(persistedSettings.VisibleFields ?? 639));
			overlaySettings2.SetCollapsedFields((DisplayField)(persistedSettings.CollapsedPrimaryField ?? 1), (DisplayField)(persistedSettings.CollapsedSecondaryField ?? 64));
			overlaySettings2.ManualPlacementEnabled = persistedSettings.ManualPlacementEnabled == true;
			overlaySettings2.MainAttachment = ManualAttachmentRules.SanitizeMain(DeserializeAttachment(persistedSettings.MainAttachment));
			overlaySettings2.OverlayScalePercent = ManualAttachmentRules.SanitizeScale(persistedSettings.OverlayScalePercent);
			overlaySettings2.DisplayBackend = SanitizeDisplayBackend(persistedSettings.DisplayBackend);
			overlaySettings2.CdpPort = SanitizeCdpPort(persistedSettings.CdpPort);
			overlaySettings2.CdpExpectedCodexVersion = SanitizeCodexVersion(persistedSettings.CdpExpectedCodexVersion);
			return new OverlaySettingsLoadResult(overlaySettings2, MustPersist: false);
		}
		catch (JsonException)
		{
			return new OverlaySettingsLoadResult(CreateDefault(), MustPersist: false);
		}
	}

	internal string Serialize()
	{
		return JsonSerializer.Serialize(new PersistedSettings
		{
			SettingsVersion = 1,
			AnchorMode = (int)SanitizeAnchorMode((int)AnchorMode),
			VisibleFields = (int)DisplayFieldRules.SanitizeVisible(VisibleFields),
			CollapsedPrimaryField = (int)CollapsedPrimaryField,
			CollapsedSecondaryField = (int)CollapsedSecondaryField,
			ManualPlacementEnabled = ManualPlacementEnabled,
			MainAttachment = SerializeAttachment(ManualAttachmentRules.SanitizeMain(MainAttachment)),
			OverlayScalePercent = ManualAttachmentRules.SanitizeScale(OverlayScalePercent),
			DisplayBackend = (int)DisplayBackend,
			CdpPort = SanitizeCdpPort(CdpPort),
			CdpExpectedCodexVersion = SanitizeCodexVersion(CdpExpectedCodexVersion)
		}, JsonOptions);
	}

	public bool TrySave(string? settingsPath = null)
	{
		string path = settingsPath ?? DefaultSettingsPath;
		try
		{
			string directoryName = Path.GetDirectoryName(path);
			if (!string.IsNullOrWhiteSpace(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
			File.WriteAllText(path, Serialize(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			return true;
		}
		catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
		{
			return false;
		}
	}

	public void Save(string? settingsPath = null)
	{
		TrySave(settingsPath);
	}

	public bool SelectCollapsedField(CollapsedSlot slot, DisplayField field)
	{
		if (!DisplayFieldRules.IsSingleSupported(field))
		{
			return false;
		}
		switch (slot)
		{
		case CollapsedSlot.Primary:
			if (CollapsedPrimaryField == field)
			{
				return false;
			}
			if (CollapsedSecondaryField == field)
			{
				DisplayField collapsedPrimaryField = CollapsedSecondaryField;
				DisplayField collapsedSecondaryField = CollapsedPrimaryField;
				CollapsedPrimaryField = collapsedPrimaryField;
				CollapsedSecondaryField = collapsedSecondaryField;
				return true;
			}
			CollapsedPrimaryField = field;
			return true;
		case CollapsedSlot.Secondary:
			if (CollapsedSecondaryField == field)
			{
				return false;
			}
			if (CollapsedPrimaryField == field)
			{
				DisplayField collapsedSecondaryField = CollapsedSecondaryField;
				DisplayField collapsedPrimaryField = CollapsedPrimaryField;
				CollapsedPrimaryField = collapsedSecondaryField;
				CollapsedSecondaryField = collapsedPrimaryField;
				return true;
			}
			CollapsedSecondaryField = field;
			return true;
		default:
			return false;
		}
	}

	public static string? ResolveSettingsOverride(IReadOnlyList<string> args)
	{
		for (int i = 0; i < args.Count; i++)
		{
			if (args[i].Equals("--settings", StringComparison.OrdinalIgnoreCase))
			{
				if (i + 1 >= args.Count || string.IsNullOrWhiteSpace(args[i + 1]))
				{
					throw new ArgumentException("--settings 后必须提供绝对设置文件路径。", "args");
				}
				string path = args[i + 1];
				if (!Path.IsPathFullyQualified(path))
				{
					throw new ArgumentException("--settings 只接受绝对设置文件路径。", "args");
				}
				return Path.GetFullPath(path);
			}
		}
		return null;
	}

	private static AnchorMode SanitizeAnchorMode(int? value)
	{
		return value switch
		{
			0 => AnchorMode.Auto, 
			1 => AnchorMode.InsideTopRight, 
			2 => AnchorMode.InsideBottomRight, 
			3 => AnchorMode.TitleBarTopRight, 
			4 => AnchorMode.ComposerBottomStrip, 
			_ => AnchorMode.TitleBarTopRight, 
		};
	}

	private static DisplayBackendKind SanitizeDisplayBackend(int? value)
	{
		return value == (int)DisplayBackendKind.ExperimentalCdp ? DisplayBackendKind.ExperimentalCdp : DisplayBackendKind.ExternalOverlay;
	}

	private static int SanitizeCdpPort(int? value)
	{
		return value is >= 1024 and <= 65535 ? value.Value : 9222;
	}

	private static string? SanitizeCodexVersion(string? value)
	{
		if (string.IsNullOrWhiteSpace(value) || !Version.TryParse(value, out _))
		{
			return null;
		}
		return value.Trim();
	}

	private static WindowAttachment? DeserializeAttachment(PersistedWindowAttachment? value)
	{
		int? num = value?.ReferencePoint;
		if (num.HasValue)
		{
			int valueOrDefault = num.GetValueOrDefault();
			double? offsetXDip = value.OffsetXDip;
			if (offsetXDip.HasValue)
			{
				double valueOrDefault2 = offsetXDip.GetValueOrDefault();
				offsetXDip = value.OffsetYDip;
				if (offsetXDip.HasValue)
				{
					double valueOrDefault3 = offsetXDip.GetValueOrDefault();
					if (!ManualAttachmentRules.TrySanitize(new WindowAttachment((AttachmentReferencePoint)valueOrDefault, valueOrDefault2, valueOrDefault3), out WindowAttachment result))
					{
						return null;
					}
					return result;
				}
			}
		}
		return null;
	}

	private static PersistedWindowAttachment? SerializeAttachment(WindowAttachment? value)
	{
		if ((object)value != null)
		{
			return new PersistedWindowAttachment
			{
				ReferencePoint = (int)value.ReferencePoint,
				OffsetXDip = value.OffsetXDip,
				OffsetYDip = value.OffsetYDip
			};
		}
		return null;
	}

	private void SetCollapsedFields(DisplayField primary, DisplayField secondary)
	{
		CollapsedPrimaryField = ((!DisplayFieldRules.IsSingleSupported(primary)) ? DisplayField.Total : primary);
		CollapsedSecondaryField = (DisplayFieldRules.IsSingleSupported(secondary) ? secondary : DisplayField.ContextPercent);
		if (CollapsedPrimaryField == CollapsedSecondaryField)
		{
			CollapsedSecondaryField = ((CollapsedPrimaryField == DisplayField.ContextPercent) ? DisplayField.Total : DisplayField.ContextPercent);
		}
	}
}
