using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CodexTokenOverlay;

internal static class OverlayPresentationBuilder
{
	private const string NoValue = "—";

	public static OverlayPresentation CreateWaiting(string statusText, DisplayField primaryField, DisplayField secondaryField, DisplayField visibleFields)
	{
		ValidateHighlightedFields(primaryField, secondaryField);
		return new OverlayPresentation(CreateWaitingMetric(primaryField), CreateWaitingMetric(secondaryField), CreateExpandedRows(visibleFields, primaryField, secondaryField, CreateWaitingMetric), 0.0, ShowContextProgress: false, SanitizeSingleLine(statusText));
	}

	public static OverlayPresentation Create(TokenSnapshot snapshot, DisplayField primaryField, DisplayField secondaryField, DisplayField visibleFields)
	{
		ArgumentNullException.ThrowIfNull(snapshot, "snapshot");
		ValidateHighlightedFields(primaryField, secondaryField);
		double contextPercent = Math.Clamp(snapshot.ContextPercent, 0.0, 100.0);
		return new OverlayPresentation(CreateMetric(snapshot, primaryField, contextPercent), CreateMetric(snapshot, secondaryField, contextPercent), CreateExpandedRows(visibleFields, primaryField, secondaryField, (DisplayField field) => CreateMetric(snapshot, field, contextPercent)), contextPercent, (visibleFields & DisplayField.ContextPercent) != 0, null);
	}

	public static string FormatTokenCount(long value)
	{
		if (value < 1000000)
		{
			if (value >= 1000)
			{
				return ((double)value / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + "k";
			}
			return value.ToString("N0", CultureInfo.InvariantCulture);
		}
		return ((double)value / 1000000.0).ToString("0.00", CultureInfo.InvariantCulture) + "M";
	}

	public static string ShortThreadId(string threadId, int maximumLength = 12)
	{
		string text = SanitizeSingleLine(threadId);
		if (text.Length <= maximumLength)
		{
			return text;
		}
		if (maximumLength <= 1)
		{
			if (maximumLength != 1)
			{
				return string.Empty;
			}
			return "…";
		}
		int num = Math.Min(4, maximumLength - 1);
		int num2 = Math.Min(6, maximumLength - num - 1);
		return string.Concat(text.AsSpan(0, num), "…".AsSpan(), text.AsSpan(text.Length - num2, num2));
	}

	public static string GetFieldMenuText(DisplayField field)
	{
		return field switch
		{
			DisplayField.Total => "总 token", 
			DisplayField.Input => "输入 token", 
			DisplayField.Output => "输出 token", 
			DisplayField.OutputRate => "本轮墙钟速度估算", 
			DisplayField.CacheHit => "缓存命中", 
			DisplayField.CacheHitRate => "缓存命中率", 
			DisplayField.CacheMiss => "缓存未命中（推导）", 
			DisplayField.Context => "上下文用量", 
			DisplayField.ContextPercent => "上下文百分比", 
			DisplayField.Reasoning => "推理输出", 
			DisplayField.Thread => "会话 ID", 
			_ => throw new ArgumentOutOfRangeException("field", field, "不支持的展示字段。"), 
		};
	}

	private static void ValidateHighlightedFields(DisplayField primaryField, DisplayField secondaryField)
	{
		if (!DisplayFieldRules.IsSingleSupported(primaryField) || !DisplayFieldRules.IsSingleSupported(secondaryField))
		{
			throw new ArgumentException("收起指标必须是受支持的单个字段。");
		}
	}

	private static IReadOnlyList<OverlayMetric> CreateExpandedRows(DisplayField visibleFields, DisplayField primaryField, DisplayField secondaryField, Func<DisplayField, OverlayMetric> createMetric)
	{
		return (from field in DisplayFieldRules.Ordered
			where (visibleFields & field) != 0
			where field != primaryField && field != secondaryField
			select field).Select(createMetric).ToArray();
	}

	private static OverlayMetric CreateWaitingMetric(DisplayField field)
	{
		(string, string) labels = GetLabels(field);
		return new OverlayMetric(field, labels.Item1, labels.Item2, "—", HasValue: false);
	}

	private static OverlayMetric CreateMetric(TokenSnapshot snapshot, DisplayField field, double contextPercent)
	{
		(string, string) labels = GetLabels(field);
		string text;
		switch (field)
		{
		case DisplayField.Total:
			text = FormatTokenCount(snapshot.TotalTokens);
			break;
		case DisplayField.Input:
			text = FormatTokenCount(snapshot.InputTokens);
			break;
		case DisplayField.Output:
			text = FormatTokenCount(snapshot.OutputTokens);
			break;
		case DisplayField.OutputRate:
		{
			double? outputTokensPerSecond = snapshot.OutputTokensPerSecond;
			object obj;
			if (outputTokensPerSecond.HasValue)
			{
				double valueOrDefault = outputTokensPerSecond.GetValueOrDefault();
				obj = $"{valueOrDefault:0.0} tok/s";
			}
			else
			{
				obj = "—";
			}
			text = (string)obj;
			break;
		}
		case DisplayField.CacheHit:
			text = FormatTokenCount(snapshot.CachedInputTokens);
			break;
		case DisplayField.CacheHitRate:
			text = $"{snapshot.CacheHitPercent:0.0}%";
			break;
		case DisplayField.CacheMiss:
			text = FormatTokenCount(snapshot.UncachedInputTokens);
			break;
		case DisplayField.Context:
			text = FormatTokenCount(snapshot.ContextUsedTokens) + " / " + FormatTokenCount(snapshot.ContextWindowTokens);
			break;
		case DisplayField.ContextPercent:
			text = $"{contextPercent:0}%";
			break;
		case DisplayField.Reasoning:
			text = FormatTokenCount(snapshot.ReasoningOutputTokens);
			break;
		case DisplayField.Thread:
			text = ShortThreadId(snapshot.ThreadId);
			break;
		default:
			throw new ArgumentOutOfRangeException("field", field, "不支持的展示字段。");
		}
		string value = text;
		bool hasValue = field switch
		{
			DisplayField.Thread => !string.IsNullOrWhiteSpace(snapshot.ThreadId), 
			DisplayField.OutputRate => snapshot.OutputTokensPerSecond.HasValue, 
			_ => true, 
		};
		return new OverlayMetric(field, labels.Item1, labels.Item2, value, hasValue);
	}

	private static (string Compact, string Expanded) GetLabels(DisplayField field)
	{
		return field switch
		{
			DisplayField.Total => (Compact: "总", Expanded: "总 Token"), 
			DisplayField.Input => (Compact: "入", Expanded: "输入"), 
			DisplayField.Output => (Compact: "出", Expanded: "输出"), 
			DisplayField.OutputRate => (Compact: "约", Expanded: "本轮墙钟速度估算"), 
			DisplayField.CacheHit => (Compact: "命中", Expanded: "缓存命中"), 
			DisplayField.CacheHitRate => (Compact: "命中率", Expanded: "缓存命中率"), 
			DisplayField.CacheMiss => (Compact: "未中", Expanded: "缓存未命中"), 
			DisplayField.Context => (Compact: "上下文", Expanded: "上下文用量"), 
			DisplayField.ContextPercent => (Compact: "上下文", Expanded: "上下文占用"), 
			DisplayField.Reasoning => (Compact: "推理", Expanded: "推理输出"), 
			DisplayField.Thread => (Compact: "会话", Expanded: "会话"), 
			_ => throw new ArgumentOutOfRangeException("field", field, "不支持的展示字段。"), 
		};
	}

	private static string SanitizeSingleLine(string value)
	{
		return value.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal);
	}
}
