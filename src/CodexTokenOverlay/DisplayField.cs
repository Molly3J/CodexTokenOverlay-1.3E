using System;

namespace CodexTokenOverlay;

[Flags]
internal enum DisplayField
{
	None = 0,
	Total = 1,
	Input = 2,
	Output = 4,
	CacheHit = 8,
	CacheMiss = 0x10,
	Context = 0x20,
	ContextPercent = 0x40,
	Reasoning = 0x80,
	Thread = 0x100,
	CacheHitRate = 0x200,
	OutputRate = 0x400
}
