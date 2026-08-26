namespace CodexTokenOverlay;

internal sealed record InProcessDisplayPayload(
	bool Visible,
	string ThreadLabel,
	string Total,
	string Input,
	string Output,
	string Cache,
	string Context,
	string ContextPercent,
	string Rate)
{
	public static InProcessDisplayPayload FromSnapshot(TokenSnapshot? snapshot, bool visible)
	{
		if (snapshot == null)
		{
			return new InProcessDisplayPayload(visible, "等待会话", "—", "—", "—", "—", "—", "—", "—");
		}
		string rate = snapshot.OutputTokensPerSecond.HasValue ? $"{snapshot.OutputTokensPerSecond.Value:0.0} t/s" : "—";
		return new InProcessDisplayPayload(
			visible,
			OverlayPresentationBuilder.ShortThreadId(snapshot.ThreadId),
			OverlayPresentationBuilder.FormatTokenCount(snapshot.TotalTokens),
			OverlayPresentationBuilder.FormatTokenCount(snapshot.InputTokens),
			OverlayPresentationBuilder.FormatTokenCount(snapshot.OutputTokens),
			$"{snapshot.CacheHitPercent:0.#}%",
			$"{OverlayPresentationBuilder.FormatTokenCount(snapshot.ContextUsedTokens)} / {OverlayPresentationBuilder.FormatTokenCount(snapshot.ContextWindowTokens)}",
			$"{snapshot.ContextPercent:0.#}%",
			rate);
	}
}
