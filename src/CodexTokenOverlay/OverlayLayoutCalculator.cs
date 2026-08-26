using System;
using System.Drawing;

namespace CodexTokenOverlay;

internal static class OverlayLayoutCalculator
{
	private const int CollapsedWidthDip = 196;

	private const int TitleBarCollapsedWidthDip = 240;

	private const int PrimaryOnlyWidthDip = 116;

	private const int CollapsedHeightDip = 34;

	private const int ExpandedWidthDip = 270;

	private const int HorizontalStripPreferredWidthDip = 1050;

	private const int HorizontalStripMinimumWidthDip = 520;

	private const int HorizontalStripHeightDip = 28;

	private const int HorizontalStripSideMarginDip = 64;

	private const int HorizontalStripBottomInsetDip = 4;

	private const int HorizontalStripComposerGapDip = 2;

	private const int CapsulePanelGapDip = 6;

	private const int CaptionSafetyGapDip = 8;

	private const int TitleLeftReserveDip = 160;

	private const int PanelChromeHeightDip = 122;

	private const int NormalRowHeightDip = 30;

	private const int MinimumRowHeightDip = 24;

	private const int LegacyOutsideGapDip = 10;

	private const int LegacyInsideMarginDip = 18;

	private const int LegacyHeaderOffsetDip = 56;

	private const int LegacyBottomOffsetDip = 70;

	public static OverlayLayoutResult Calculate(OverlayLayoutRequest request)
	{
		ArgumentNullException.ThrowIfNull(request, "request");
		ArgumentNullException.ThrowIfNull(request.HostWindow, "request.HostWindow");
		if (request.ManualCapsuleCenter.HasValue)
		{
			return CalculateManual(request);
		}
		if (request.AnchorMode == AnchorMode.ComposerBottomStrip)
		{
			return CalculateComposerBottomStrip(request);
		}
		if (request.AnchorMode != AnchorMode.TitleBarTopRight)
		{
			return CalculateLegacy(request);
		}
		return CalculateTitleBar(request);
	}

	private static OverlayLayoutResult CalculateComposerBottomStrip(OverlayLayoutRequest request)
	{
		CodexWindowInfo hostWindow = request.HostWindow;
		uint dpi = ((hostWindow.Dpi == 0) ? 96u : hostWindow.Dpi);
		int scalePercent = ManualAttachmentRules.SanitizeScale(request.ScalePercent);
		IntRect second = Intersect(PreferredHostBounds(hostWindow), hostWindow.WorkingArea);
		if (second.IsEmpty)
		{
			return Hidden(hostWindow.Dpi, ExpansionDirection.Up, scalePercent);
		}
		int num = ScaleSystemDip(64.0, dpi);
		IntRect? composerBounds = request.ComposerBounds;
		IntRect intRect;
		if (composerBounds.HasValue)
		{
			IntRect valueOrDefault = composerBounds.GetValueOrDefault();
			intRect = Intersect(valueOrDefault, second);
		}
		else
		{
			intRect = default(IntRect);
		}
		IntRect intRect2 = intRect;
		int num2 = (intRect2.IsEmpty ? (second.Width - 2 * num) : intRect2.Width);
		int num3 = ScaleOverlayDip(520.0, dpi, scalePercent);
		int num4 = ScaleOverlayDip(28.0, dpi, scalePercent);
		if (num2 < num3 || second.Height < num4)
		{
			return Hidden(hostWindow.Dpi, ExpansionDirection.Up, scalePercent);
		}
		int num5 = Math.Min(num2, ScaleOverlayDip(1050.0, dpi, scalePercent));
		int num6 = ScaleSystemDip(4.0, dpi);
		int value = (intRect2.IsEmpty ? (second.Left + (second.Width - num5) / 2) : (intRect2.Left + (intRect2.Width - num5) / 2));
		int value2 = (intRect2.IsEmpty ? (second.Bottom - num4 - num6) : (intRect2.Bottom + ScaleSystemDip(2.0, dpi)));
		return Collapsed(capsuleScreen: new IntRect(Clamp(value, second.Left, second.Right - num5), Clamp(value2, second.Top, second.Bottom - num4 - num6), num5, num4), dpi: hostWindow.Dpi, display: CollapsedDisplayMode.HorizontalStrip, direction: ExpansionDirection.Up, scalePercent: scalePercent);
	}

	public static Size GetCollapsedSize(uint dpi, int scalePercent, CollapsedDisplayMode display)
	{
		return new Size(ScaleOverlayDip((display == CollapsedDisplayMode.TwoFields) ? 196 : 116, dpi, scalePercent), ScaleOverlayDip(34.0, dpi, scalePercent));
	}

	private static Size GetTitleBarCollapsedSize(uint dpi, int scalePercent, CollapsedDisplayMode display)
	{
		if (display != CollapsedDisplayMode.TwoFields)
		{
			return GetCollapsedSize(dpi, scalePercent, display);
		}
		return new Size(ScaleOverlayDip(240.0, dpi, scalePercent), ScaleOverlayDip(34.0, dpi, scalePercent));
	}

	private static OverlayLayoutResult CalculateManual(OverlayLayoutRequest request)
	{
		CodexWindowInfo hostWindow = request.HostWindow;
		int scalePercent = ManualAttachmentRules.SanitizeScale(request.ScalePercent);
		IntRect workingArea = hostWindow.WorkingArea;
		if (hostWindow.Dpi == 0 || workingArea.IsEmpty)
		{
			return Hidden(hostWindow.Dpi, ExpansionDirection.Down, scalePercent);
		}
		CollapsedDisplayMode collapsedDisplayMode = CollapsedDisplayMode.TwoFields;
		Size collapsedSize = GetCollapsedSize(hostWindow.Dpi, scalePercent, collapsedDisplayMode);
		if (collapsedSize.Width > workingArea.Width)
		{
			collapsedDisplayMode = CollapsedDisplayMode.PrimaryOnly;
			collapsedSize = GetCollapsedSize(hostWindow.Dpi, scalePercent, collapsedDisplayMode);
		}
		if (collapsedSize.Width > workingArea.Width || collapsedSize.Height > workingArea.Height)
		{
			return Hidden(hostWindow.Dpi, ExpansionDirection.Down, scalePercent);
		}
		Point value = request.ManualCapsuleCenter.Value;
		IntRect intRect = new IntRect(Clamp(value.X - collapsedSize.Width / 2, workingArea.Left, workingArea.Right - collapsedSize.Width), Clamp(value.Y - collapsedSize.Height / 2, workingArea.Top, workingArea.Bottom - collapsedSize.Height), collapsedSize.Width, collapsedSize.Height);
		OverlayLayoutResult result = Collapsed(hostWindow.Dpi, collapsedDisplayMode, ExpansionDirection.Down, intRect, scalePercent);
		if (!request.RequestExpanded)
		{
			return result;
		}
		int num = ScaleOverlayDip(270.0, hostWindow.Dpi, scalePercent);
		if (num > workingArea.Width)
		{
			return result;
		}
		int num2 = workingArea.Right;
		IntRect visibleHost = Intersect(PreferredHostBounds(hostWindow), workingArea);
		int safetyGap = ScaleSystemDip(8.0, hostWindow.Dpi);
		if (!visibleHost.IsEmpty && TryGetCaptionRegion(hostWindow, visibleHost, safetyGap, out var captionTop, out var captionBottom, out var safeRight) && intRect.Top < captionBottom && intRect.Bottom > captionTop)
		{
			num2 = Math.Min(num2, safeRight);
		}
		int num3 = workingArea.Left + num - intRect.Width;
		int num4 = num2 - intRect.Width;
		if (num4 < num3)
		{
			return result;
		}
		intRect = intRect with
		{
			X = Clamp(intRect.X, num3, num4)
		};
		int num5 = intRect.Right - num;
		int num6 = ScaleOverlayDip(6.0, hostWindow.Dpi, scalePercent);
		int availableHeight = workingArea.Bottom - intRect.Bottom - num6;
		if (TryGetPanelSize(request, hostWindow.Dpi, scalePercent, availableHeight, out var panelHeight, out var rowHeight))
		{
			int num7 = intRect.Bottom + num6;
			return new OverlayLayoutResult(OverlayVisualState.Expanded, collapsedDisplayMode, ExpansionDirection.Down, hostWindow.Dpi, new IntRect(num5, intRect.Top, num, num7 + panelHeight - intRect.Top), new IntRect(intRect.Left - num5, 0, intRect.Width, intRect.Height), new IntRect(0, num7 - intRect.Top, num, panelHeight), rowHeight, scalePercent);
		}
		int availableHeight2 = intRect.Top - num6 - workingArea.Top;
		if (TryGetPanelSize(request, hostWindow.Dpi, scalePercent, availableHeight2, out panelHeight, out rowHeight))
		{
			int num8 = intRect.Top - num6 - panelHeight;
			return new OverlayLayoutResult(OverlayVisualState.Expanded, collapsedDisplayMode, ExpansionDirection.Up, hostWindow.Dpi, new IntRect(num5, num8, num, intRect.Bottom - num8), new IntRect(intRect.Left - num5, panelHeight + num6, intRect.Width, intRect.Height), new IntRect(0, 0, num, panelHeight), rowHeight, scalePercent);
		}
		return result;
	}

	private static OverlayLayoutResult CalculateTitleBar(OverlayLayoutRequest request)
	{
		CodexWindowInfo hostWindow = request.HostWindow;
		int num = ManualAttachmentRules.SanitizeScale(request.ScalePercent);
		if (hostWindow.Dpi == 0)
		{
			return Hidden(hostWindow.Dpi, ExpansionDirection.Down, num);
		}
		IntRect workingArea = hostWindow.WorkingArea;
		IntRect visibleHost = Intersect(PreferredHostBounds(hostWindow), workingArea);
		if (visibleHost.IsEmpty)
		{
			return Hidden(hostWindow.Dpi, ExpansionDirection.Down, num);
		}
		int safetyGap = ScaleSystemDip(8.0, hostWindow.Dpi);
		if (!TryGetCaptionRegion(hostWindow, visibleHost, safetyGap, out var captionTop, out var captionBottom, out var safeRight))
		{
			return Hidden(hostWindow.Dpi, ExpansionDirection.Down, num);
		}
		safeRight = Math.Min(safeRight, Math.Min(visibleHost.Right, workingArea.Right));
		int num2 = Math.Max(visibleHost.Left, workingArea.Left) + ScaleSystemDip(160.0, hostWindow.Dpi);
		int availableWidth = safeRight - num2;
		int num3 = captionBottom - captionTop;
		if (!TryGetLargestTitleBarFit(hostWindow.Dpi, num, availableWidth, num3, out var effectiveScale, out var display, out var capsuleSize))
		{
			return Hidden(hostWindow.Dpi, ExpansionDirection.Down, num);
		}
		IntRect capsuleScreen = new IntRect(safeRight - capsuleSize.Width, captionTop + (num3 - capsuleSize.Height) / 2, capsuleSize.Width, capsuleSize.Height);
		OverlayLayoutResult result = Collapsed(hostWindow.Dpi, display, ExpansionDirection.Down, capsuleScreen, effectiveScale);
		if (!request.RequestExpanded)
		{
			return result;
		}
		int num4 = Math.Max(ScaleOverlayDip(270.0, hostWindow.Dpi, effectiveScale), capsuleSize.Width);
		int num5 = capsuleScreen.Right - num4;
		if (num5 < workingArea.Left || capsuleScreen.Right > workingArea.Right)
		{
			return result;
		}
		int num6 = capsuleScreen.Bottom + ScaleOverlayDip(6.0, hostWindow.Dpi, effectiveScale);
		int availableHeight = workingArea.Bottom - num6;
		if (!TryGetPanelSize(request, hostWindow.Dpi, effectiveScale, availableHeight, out var panelHeight, out var rowHeight))
		{
			return result;
		}
		return new OverlayLayoutResult(WindowBounds: new IntRect(num5, capsuleScreen.Top, num4, num6 + panelHeight - capsuleScreen.Top), State: OverlayVisualState.Expanded, CollapsedDisplay: display, ExpansionDirection: ExpansionDirection.Down, Dpi: hostWindow.Dpi, CapsuleBounds: new IntRect(capsuleScreen.Left - num5, 0, capsuleSize.Width, capsuleSize.Height), PanelBounds: new IntRect(0, num6 - capsuleScreen.Top, num4, panelHeight), ExpandedRowHeight: rowHeight, ScalePercent: effectiveScale);
	}

	private static bool TryGetLargestTitleBarFit(uint dpi, int requestedScale, int availableWidth, int captionHeight, out int effectiveScale, out CollapsedDisplayMode display, out Size capsuleSize)
	{
		requestedScale = ManualAttachmentRules.SanitizeScale(requestedScale);
		for (int num = requestedScale; num >= 60; num--)
		{
			Size titleBarCollapsedSize = GetTitleBarCollapsedSize(dpi, num, CollapsedDisplayMode.TwoFields);
			if (titleBarCollapsedSize.Width <= availableWidth && titleBarCollapsedSize.Height <= captionHeight)
			{
				effectiveScale = num;
				display = CollapsedDisplayMode.TwoFields;
				capsuleSize = titleBarCollapsedSize;
				return true;
			}
			Size collapsedSize = GetCollapsedSize(dpi, num, CollapsedDisplayMode.PrimaryOnly);
			if (collapsedSize.Width <= availableWidth && collapsedSize.Height <= captionHeight)
			{
				effectiveScale = num;
				display = CollapsedDisplayMode.PrimaryOnly;
				capsuleSize = collapsedSize;
				return true;
			}
		}
		effectiveScale = requestedScale;
		display = CollapsedDisplayMode.TwoFields;
		capsuleSize = Size.Empty;
		return false;
	}

	private static OverlayLayoutResult CalculateLegacy(OverlayLayoutRequest request)
	{
		CodexWindowInfo hostWindow = request.HostWindow;
		uint dpi = ((hostWindow.Dpi == 0) ? 96u : hostWindow.Dpi);
		int scalePercent = ManualAttachmentRules.SanitizeScale(request.ScalePercent);
		IntRect workingArea = hostWindow.WorkingArea;
		IntRect intRect = Intersect(PreferredHostBounds(hostWindow), workingArea);
		if (intRect.IsEmpty || workingArea.IsEmpty)
		{
			return Hidden(hostWindow.Dpi, DirectionFor(request.AnchorMode), scalePercent);
		}
		int num = ScaleSystemDip(10.0, dpi);
		int num2 = ScaleSystemDip(18.0, dpi);
		Size collapsedSize = GetCollapsedSize(dpi, scalePercent, CollapsedDisplayMode.TwoFields);
		Size collapsedSize2 = GetCollapsedSize(dpi, scalePercent, CollapsedDisplayMode.PrimaryOnly);
		int height = collapsedSize.Height;
		int width = collapsedSize.Width;
		int width2 = collapsedSize2.Width;
		int num3 = Math.Max(0, workingArea.Width - 2 * num);
		CollapsedDisplayMode collapsedDisplayMode = ((num3 < width) ? CollapsedDisplayMode.PrimaryOnly : CollapsedDisplayMode.TwoFields);
		int num4 = ((collapsedDisplayMode == CollapsedDisplayMode.TwoFields) ? width : width2);
		if (num3 < width2 || workingArea.Height < height)
		{
			return Hidden(hostWindow.Dpi, DirectionFor(request.AnchorMode), scalePercent);
		}
		AnchorMode anchorMode = request.AnchorMode;
		bool num5 = anchorMode == AnchorMode.Auto;
		bool flag = false;
		bool flag2 = false;
		if (anchorMode == AnchorMode.Auto)
		{
			int num6 = (request.RequestExpanded ? ScaleOverlayDip(270.0, dpi, scalePercent) : num4);
			if (workingArea.Right - intRect.Right >= num6 + num)
			{
				flag = true;
			}
			else if (intRect.Left - workingArea.Left >= num6 + num)
			{
				flag2 = true;
			}
			else
			{
				anchorMode = AnchorMode.InsideTopRight;
			}
		}
		int value;
		int value2;
		if (flag)
		{
			value = intRect.Right + num;
			value2 = Math.Max(workingArea.Top + num, intRect.Bottom - height - ScaleSystemDip(70.0, dpi));
		}
		else if (flag2)
		{
			value = intRect.Left - num4 - num;
			value2 = Math.Max(workingArea.Top + num, intRect.Bottom - height - ScaleSystemDip(70.0, dpi));
		}
		else if (anchorMode == AnchorMode.InsideBottomRight)
		{
			value = intRect.Right - num4 - num2;
			value2 = intRect.Bottom - height - num2;
		}
		else
		{
			value = intRect.Right - num4 - num2;
			value2 = intRect.Top + ScaleSystemDip(56.0, dpi);
		}
		value = Clamp(value, workingArea.Left + num, workingArea.Right - num4 - num);
		value2 = Clamp(value2, workingArea.Top + num, workingArea.Bottom - height - num);
		IntRect intRect2 = new IntRect(value, value2, num4, height);
		ExpansionDirection expansionDirection = (num5 ? ChooseDirection(request, intRect2, workingArea, dpi, scalePercent) : ((anchorMode == AnchorMode.InsideBottomRight) ? ExpansionDirection.Up : ExpansionDirection.Down));
		OverlayLayoutResult result = Collapsed(hostWindow.Dpi, collapsedDisplayMode, expansionDirection, intRect2, scalePercent);
		if (!request.RequestExpanded)
		{
			return result;
		}
		int num7 = ScaleOverlayDip(270.0, dpi, scalePercent);
		int num8 = intRect2.Right - num7;
		if (num8 < workingArea.Left || intRect2.Right > workingArea.Right)
		{
			return result;
		}
		int num9 = ScaleOverlayDip(6.0, dpi, scalePercent);
		int availableHeight = ((expansionDirection == ExpansionDirection.Down) ? (workingArea.Bottom - intRect2.Bottom - num9) : (intRect2.Top - num9 - workingArea.Top));
		if (!TryGetPanelSize(request, dpi, scalePercent, availableHeight, out var panelHeight, out var rowHeight))
		{
			return result;
		}
		if (expansionDirection == ExpansionDirection.Down)
		{
			int num10 = intRect2.Bottom + num9;
			return new OverlayLayoutResult(WindowBounds: new IntRect(num8, intRect2.Top, num7, num10 + panelHeight - intRect2.Top), State: OverlayVisualState.Expanded, CollapsedDisplay: collapsedDisplayMode, ExpansionDirection: expansionDirection, Dpi: hostWindow.Dpi, CapsuleBounds: new IntRect(intRect2.Left - num8, 0, num4, height), PanelBounds: new IntRect(0, num10 - intRect2.Top, num7, panelHeight), ExpandedRowHeight: rowHeight, ScalePercent: scalePercent);
		}
		int num11 = intRect2.Top - num9 - panelHeight;
		return new OverlayLayoutResult(WindowBounds: new IntRect(num8, num11, num7, intRect2.Bottom - num11), State: OverlayVisualState.Expanded, CollapsedDisplay: collapsedDisplayMode, ExpansionDirection: expansionDirection, Dpi: hostWindow.Dpi, CapsuleBounds: new IntRect(intRect2.Left - num8, panelHeight + num9, num4, height), PanelBounds: new IntRect(0, 0, num7, panelHeight), ExpandedRowHeight: rowHeight, ScalePercent: scalePercent);
	}

	private static bool TryGetCaptionRegion(CodexWindowInfo host, IntRect visibleHost, int safetyGap, out int captionTop, out int captionBottom, out int safeRight)
	{
		IntRect? captionButtonBounds = host.CaptionButtonBounds;
		if (captionButtonBounds.HasValue)
		{
			IntRect valueOrDefault = captionButtonBounds.GetValueOrDefault();
			if (!valueOrDefault.IsEmpty && valueOrDefault.Left >= visibleHost.Left && valueOrDefault.Left < visibleHost.Right && valueOrDefault.Bottom > visibleHost.Top && valueOrDefault.Top < visibleHost.Bottom)
			{
				captionTop = Math.Max(valueOrDefault.Top, visibleHost.Top);
				captionBottom = Math.Min(valueOrDefault.Bottom, visibleHost.Bottom);
				safeRight = valueOrDefault.Left - safetyGap;
				return captionBottom > captionTop;
			}
		}
		WindowChromeMetrics chromeMetrics = host.ChromeMetrics;
		if (chromeMetrics.CaptionButtonWidth <= 0 || chromeMetrics.CaptionButtonHeight <= 0 || chromeMetrics.FrameWidth < 0 || chromeMetrics.FrameHeight < 0 || chromeMetrics.PaddedBorderWidth < 0)
		{
			captionTop = 0;
			captionBottom = 0;
			safeRight = 0;
			return false;
		}
		int num = 3 * chromeMetrics.CaptionButtonWidth + 2 * chromeMetrics.FrameWidth + 2 * chromeMetrics.PaddedBorderWidth;
		int num2 = host.WindowBounds.Top + chromeMetrics.FrameHeight + chromeMetrics.PaddedBorderWidth;
		int val = num2 + chromeMetrics.CaptionButtonHeight;
		captionTop = Math.Max(visibleHost.Top, num2);
		captionBottom = Math.Min(visibleHost.Bottom, val);
		safeRight = host.WindowBounds.Right - num - safetyGap;
		if (captionBottom > captionTop)
		{
			return safeRight > visibleHost.Left;
		}
		return false;
	}

	private static bool TryGetPanelSize(OverlayLayoutRequest request, uint dpi, int scalePercent, int availableHeight, out int panelHeight, out int rowHeight)
	{
		int num = Math.Max(0, request.ExpandedRowCount);
		int num2 = ScaleOverlayDip(122.0, dpi, scalePercent);
		int num3 = ScaleOverlayDip(30.0, dpi, scalePercent);
		int num4 = ScaleOverlayDip(24.0, dpi, scalePercent);
		rowHeight = num3;
		panelHeight = num2 + num * rowHeight;
		if (panelHeight <= availableHeight)
		{
			return true;
		}
		if (num == 0)
		{
			rowHeight = 0;
			panelHeight = 0;
			return false;
		}
		rowHeight = (availableHeight - num2) / num;
		if (rowHeight < num4)
		{
			rowHeight = 0;
			panelHeight = 0;
			return false;
		}
		rowHeight = Math.Min(rowHeight, num3);
		panelHeight = num2 + num * rowHeight;
		return panelHeight <= availableHeight;
	}

	private static ExpansionDirection ChooseDirection(OverlayLayoutRequest request, IntRect capsule, IntRect workingArea, uint dpi, int scalePercent)
	{
		int num = ScaleOverlayDip(6.0, dpi, scalePercent);
		int num2 = workingArea.Bottom - capsule.Bottom - num;
		int num3 = capsule.Top - num - workingArea.Top;
		int num4 = Math.Max(0, request.ExpandedRowCount);
		int num5 = ScaleOverlayDip(122.0, dpi, scalePercent) + num4 * ScaleOverlayDip(30.0, dpi, scalePercent);
		if (num2 >= num5)
		{
			return ExpansionDirection.Down;
		}
		if (num3 >= num5)
		{
			return ExpansionDirection.Up;
		}
		int num6 = ScaleOverlayDip(122.0, dpi, scalePercent) + num4 * ScaleOverlayDip(24.0, dpi, scalePercent);
		bool flag = num2 >= num6;
		bool flag2 = num3 >= num6;
		if (flag != flag2)
		{
			if (!flag)
			{
				return ExpansionDirection.Up;
			}
			return ExpansionDirection.Down;
		}
		if (num2 < num3)
		{
			return ExpansionDirection.Up;
		}
		return ExpansionDirection.Down;
	}

	private static ExpansionDirection DirectionFor(AnchorMode anchorMode)
	{
		if (anchorMode != AnchorMode.InsideBottomRight)
		{
			return ExpansionDirection.Down;
		}
		return ExpansionDirection.Up;
	}

	private static OverlayLayoutResult Collapsed(uint dpi, CollapsedDisplayMode display, ExpansionDirection direction, IntRect capsuleScreen, int scalePercent)
	{
		return new OverlayLayoutResult(OverlayVisualState.Collapsed, display, direction, dpi, capsuleScreen, new IntRect(0, 0, capsuleScreen.Width, capsuleScreen.Height), default(IntRect), 0, ManualAttachmentRules.SanitizeScale(scalePercent));
	}

	private static OverlayLayoutResult Hidden(uint dpi, ExpansionDirection direction, int scalePercent)
	{
		return new OverlayLayoutResult(OverlayVisualState.HiddenForSpace, CollapsedDisplayMode.TwoFields, direction, dpi, default(IntRect), default(IntRect), default(IntRect), 0, ManualAttachmentRules.SanitizeScale(scalePercent));
	}

	private static IntRect PreferredHostBounds(CodexWindowInfo host)
	{
		if (!host.ExtendedFrameBounds.IsEmpty)
		{
			return host.ExtendedFrameBounds;
		}
		return host.WindowBounds;
	}

	private static IntRect Intersect(IntRect first, IntRect second)
	{
		int num = Math.Max(first.Left, second.Left);
		int num2 = Math.Max(first.Top, second.Top);
		int num3 = Math.Min(first.Right, second.Right);
		int num4 = Math.Min(first.Bottom, second.Bottom);
		if (num3 > num && num4 > num2)
		{
			return new IntRect(num, num2, num3 - num, num4 - num2);
		}
		return default(IntRect);
	}

	private static int Clamp(int value, int minimum, int maximum)
	{
		if (maximum >= minimum)
		{
			return Math.Clamp(value, minimum, maximum);
		}
		return minimum;
	}

	private static int ScaleSystemDip(double dip, uint dpi)
	{
		return (int)Math.Round(dip * (double)dpi / 96.0, MidpointRounding.AwayFromZero);
	}

	private static int ScaleOverlayDip(double dip, uint dpi, int scalePercent)
	{
		return (int)Math.Round(dip * (double)dpi / 96.0 * (double)ManualAttachmentRules.SanitizeScale(scalePercent) / 100.0, MidpointRounding.AwayFromZero);
	}
}
