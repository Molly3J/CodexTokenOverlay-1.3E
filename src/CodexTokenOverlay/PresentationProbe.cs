using System;
using System.Collections.Generic;

namespace CodexTokenOverlay;

internal static class PresentationProbe
{
	public static PresentationProbeResult Execute(PresentationProbeRequest request)
	{
		ArgumentNullException.ThrowIfNull(request, "request");
		List<PresentationProbeCaseResult> list = new List<PresentationProbeCaseResult>();
		foreach (PresentationProbeCase @case in request.Cases)
		{
			DisplayField primaryField = RequireField(@case.PrimaryField, "PrimaryField");
			DisplayField secondaryField = RequireField(@case.SecondaryField, "SecondaryField");
			DisplayField valueOrDefault = (DisplayField)@case.VisibleFields.GetValueOrDefault();
			string operation = @case.Operation;
			OverlayPresentation overlayPresentation;
			if (!(operation == "Create"))
			{
				if (!(operation == "Waiting"))
				{
					throw new ArgumentException("不支持的展示探针操作：" + @case.Operation, "probeCase");
				}
				overlayPresentation = OverlayPresentationBuilder.CreateWaiting(@case.StatusText ?? string.Empty, primaryField, secondaryField, valueOrDefault);
			}
			else
			{
				overlayPresentation = OverlayPresentationBuilder.Create(@case.Snapshot ?? throw new ArgumentException("Create 操作需要 Snapshot。", "probeCase"), primaryField, secondaryField, valueOrDefault);
			}
			OverlayPresentation presentation = overlayPresentation;
			list.Add(new PresentationProbeCaseResult(@case.Name, presentation, TokenStripForm.BuildHorizontalStripText(presentation)));
		}
		return new PresentationProbeResult(list);
	}

	private static DisplayField RequireField(int? value, string parameterName)
	{
		if (!value.HasValue)
		{
			throw new ArgumentException("展示探针需要字段。", parameterName);
		}
		return (DisplayField)value.Value;
	}
}
