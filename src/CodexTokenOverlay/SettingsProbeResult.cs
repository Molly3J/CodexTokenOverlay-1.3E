using System.Collections.Generic;

namespace CodexTokenOverlay;

internal sealed record SettingsProbeResult(IReadOnlyList<SettingsProbeCaseResult> Cases);
