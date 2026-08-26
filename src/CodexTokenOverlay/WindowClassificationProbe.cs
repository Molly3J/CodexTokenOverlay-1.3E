using System;
using System.Linq;

namespace CodexTokenOverlay;

internal static class WindowClassificationProbe
{
	public static WindowClassificationProbeResult Execute(WindowClassificationProbeRequest request)
	{
		ArgumentNullException.ThrowIfNull(request, "request");
		return new WindowClassificationProbeResult(request.Cases.Select(delegate(WindowClassificationProbeCaseRequest item)
		{
			CodexWindowCandidateSelection codexWindowCandidateSelection = CodexWindowClassifier.Select(item.Candidates.Select((WindowCandidateFactsProbe candidate) => candidate.ToModel()).ToArray(), new IntPtr(item.ForegroundHandle));
			return new WindowClassificationProbeCaseResult(item.Name, ((IntPtr)codexWindowCandidateSelection?.Host.Handle).ToInt64());
		}).ToArray());
	}
}
