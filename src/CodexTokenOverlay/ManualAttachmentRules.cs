using System;

namespace CodexTokenOverlay;

internal static class ManualAttachmentRules
{
	public const int MinimumScalePercent = 60;

	public const int MaximumScalePercent = 130;

	public const int DefaultScalePercent = 100;

	public const double MaximumAbsoluteOffsetDip = 4096.0;

	public static readonly WindowAttachment DefaultMainAttachment = new WindowAttachment(AttachmentReferencePoint.TopRight, -344.0, 24.0);

	public static int SanitizeScale(int? value)
	{
		return Math.Clamp(value ?? 100, 60, 130);
	}

	public static WindowAttachment SanitizeMain(WindowAttachment? value)
	{
		if (!TrySanitize(value, out WindowAttachment result))
		{
			return DefaultMainAttachment;
		}
		return result;
	}

	public static bool TrySanitize(WindowAttachment? value, out WindowAttachment result)
	{
		if ((object)value != null && Enum.IsDefined(value.ReferencePoint) && double.IsFinite(value.OffsetXDip) && double.IsFinite(value.OffsetYDip) && Math.Abs(value.OffsetXDip) <= 4096.0 && Math.Abs(value.OffsetYDip) <= 4096.0)
		{
			result = new WindowAttachment(value.ReferencePoint, value.OffsetXDip, value.OffsetYDip);
			return true;
		}
		result = null;
		return false;
	}
}
