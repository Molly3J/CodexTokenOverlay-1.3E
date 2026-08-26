using System.Collections.Generic;

namespace CodexTokenOverlay;

internal static class DisplayFieldRules
{
	public const DisplayField SupportedMask = DisplayField.Total | DisplayField.Input | DisplayField.Output | DisplayField.CacheHit | DisplayField.CacheMiss | DisplayField.Context | DisplayField.ContextPercent | DisplayField.Reasoning | DisplayField.Thread | DisplayField.CacheHitRate | DisplayField.OutputRate;

	public static readonly IReadOnlyList<DisplayField> Ordered = new DisplayField[11]
	{
		DisplayField.Total,
		DisplayField.Input,
		DisplayField.Output,
		DisplayField.OutputRate,
		DisplayField.CacheHit,
		DisplayField.CacheHitRate,
		DisplayField.CacheMiss,
		DisplayField.Context,
		DisplayField.ContextPercent,
		DisplayField.Reasoning,
		DisplayField.Thread
	};

	public static bool IsSingleSupported(DisplayField field)
	{
		if (field > DisplayField.None && (field & (field - 1)) == 0)
		{
			return (field & (DisplayField.Total | DisplayField.Input | DisplayField.Output | DisplayField.CacheHit | DisplayField.CacheMiss | DisplayField.Context | DisplayField.ContextPercent | DisplayField.Reasoning | DisplayField.Thread | DisplayField.CacheHitRate | DisplayField.OutputRate)) == field;
		}
		return false;
	}

	public static DisplayField SanitizeVisible(DisplayField fields)
	{
		DisplayField displayField = fields & (DisplayField.Total | DisplayField.Input | DisplayField.Output | DisplayField.CacheHit | DisplayField.CacheMiss | DisplayField.Context | DisplayField.ContextPercent | DisplayField.Reasoning | DisplayField.Thread | DisplayField.CacheHitRate | DisplayField.OutputRate);
		if (displayField != DisplayField.None)
		{
			return displayField;
		}
		return DisplayField.Total;
	}
}
