using System;

namespace CodexTokenOverlay;

internal sealed record TokenSnapshot(string ThreadId, string LogPath, long TotalTokens, long InputTokens, long CachedInputTokens, long OutputTokens, long ReasoningOutputTokens, long ContextUsedTokens, long ContextWindowTokens, DateTime UpdatedAtUtc)
{
	public string? ModelId { get; init; }

	public long SessionContextWindowTokens { get; init; }

	public long ModelMaxContextWindowTokens { get; init; }

	public string? ContextWindowSource { get; init; }

	public double? OutputTokensPerSecond { get; init; }

	public double ContextPercent
	{
		get
		{
			if (ContextWindowTokens > 0)
			{
				return Math.Clamp((double)ContextUsedTokens * 100.0 / (double)ContextWindowTokens, 0.0, 100.0);
			}
			return 0.0;
		}
	}

	public double CacheHitPercent
	{
		get
		{
			if (InputTokens > 0)
			{
				return Math.Clamp((double)CachedInputTokens * 100.0 / (double)InputTokens, 0.0, 100.0);
			}
			return 0.0;
		}
	}

	public long UncachedInputTokens => Math.Max(0L, InputTokens - CachedInputTokens);
}
