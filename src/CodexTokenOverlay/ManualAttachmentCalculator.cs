using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace CodexTokenOverlay;

internal static class ManualAttachmentCalculator
{
	public static AttachmentReferencePoint SelectReferencePoint(IntRect target, Point center)
	{
		ValidateTarget(target);
		return (from item in EnumerateReferencePoints(target)
			select (Kind: item.Kind, Distance: SquaredDistance(item.Point, center)) into item
			orderby item.Distance, item.Kind
			select item).First().Kind;
	}

	public static WindowAttachment Capture(IntRect target, Point center, uint dpi)
	{
		ValidateTargetAndDpi(target, dpi);
		AttachmentReferencePoint referencePoint = SelectReferencePoint(target, center);
		Point referencePoint2 = GetReferencePoint(target, referencePoint);
		return new WindowAttachment(referencePoint, (double)((long)center.X - (long)referencePoint2.X) * 96.0 / (double)dpi, (double)((long)center.Y - (long)referencePoint2.Y) * 96.0 / (double)dpi);
	}

	public static Point ResolveCenter(IntRect target, WindowAttachment attachment, uint dpi)
	{
		ArgumentNullException.ThrowIfNull(attachment, "attachment");
		ValidateTargetAndDpi(target, dpi);
		Point referencePoint = GetReferencePoint(target, attachment.ReferencePoint);
		int num = (int)Math.Round(attachment.OffsetXDip * (double)dpi / 96.0, MidpointRounding.AwayFromZero);
		int num2 = (int)Math.Round(attachment.OffsetYDip * (double)dpi / 96.0, MidpointRounding.AwayFromZero);
		return checked(new Point(referencePoint.X + num, referencePoint.Y + num2));
	}

	public static AttachmentTargetHit? SelectTarget(AttachmentTargetBounds targets, Point cursor, bool hostSurfaceHit)
	{
		if (!hostSurfaceHit || targets.MainBounds.IsEmpty || !targets.MainBounds.Contains(cursor.X, cursor.Y))
		{
			return null;
		}
		return new AttachmentTargetHit(targets.MainHandle, targets.MainBounds);
	}

	public static int CalculateScale(Size startSize, int startScalePercent, int deltaX, int deltaY)
	{
		int num = ManualAttachmentRules.SanitizeScale(startScalePercent);
		if (startSize.Width <= 0 || startSize.Height <= 0)
		{
			return num;
		}
		double val = Math.Max(0.0, ((double)startSize.Width + (double)deltaX) / (double)startSize.Width);
		double val2 = Math.Max(0.0, ((double)startSize.Height + (double)deltaY) / (double)startSize.Height);
		return ManualAttachmentRules.SanitizeScale((int)Math.Round((double)num * Math.Max(val, val2), MidpointRounding.AwayFromZero));
	}

	private static IReadOnlyList<(AttachmentReferencePoint Kind, Point Point)> EnumerateReferencePoints(IntRect target)
	{
		return new _003C_003Ez__ReadOnlyArray<(AttachmentReferencePoint, Point)>(new(AttachmentReferencePoint, Point)[8]
		{
			(AttachmentReferencePoint.TopLeft, new Point(target.Left, target.Top)),
			(AttachmentReferencePoint.TopCenter, new Point(CenterX(target), target.Top)),
			(AttachmentReferencePoint.TopRight, new Point(target.Right, target.Top)),
			(AttachmentReferencePoint.LeftCenter, new Point(target.Left, CenterY(target))),
			(AttachmentReferencePoint.RightCenter, new Point(target.Right, CenterY(target))),
			(AttachmentReferencePoint.BottomLeft, new Point(target.Left, target.Bottom)),
			(AttachmentReferencePoint.BottomCenter, new Point(CenterX(target), target.Bottom)),
			(AttachmentReferencePoint.BottomRight, new Point(target.Right, target.Bottom))
		});
	}

	private static Point GetReferencePoint(IntRect target, AttachmentReferencePoint referencePoint)
	{
		if (!Enum.IsDefined(referencePoint))
		{
			throw new ArgumentOutOfRangeException("referencePoint");
		}
		return EnumerateReferencePoints(target)[(int)referencePoint].Point;
	}

	private static int CenterX(IntRect target)
	{
		return target.Left + target.Width / 2;
	}

	private static int CenterY(IntRect target)
	{
		return target.Top + target.Height / 2;
	}

	private static long SquaredDistance(Point left, Point right)
	{
		long num = (long)left.X - (long)right.X;
		long num2 = (long)left.Y - (long)right.Y;
		return checked(num * num + num2 * num2);
	}

	private static void ValidateTargetAndDpi(IntRect target, uint dpi)
	{
		ValidateTarget(target);
		if (dpi == 0)
		{
			throw new ArgumentOutOfRangeException("dpi");
		}
	}

	private static void ValidateTarget(IntRect target)
	{
		if (target.IsEmpty)
		{
			throw new ArgumentOutOfRangeException("target");
		}
	}
}
