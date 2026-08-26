using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace CodexTokenOverlay;

internal sealed class TokenStripForm : Form
{
	private const double HorizontalStripOpacity = 0.82;

	private const int WsExToolWindow = 128;

	private const int WsExNoActivate = 134217728;

	private const int CsDropShadow = 131072;

	private const int WmMouseActivate = 33;

	private const int WmNcHitTest = 132;

	private const int MaNoActivate = 3;

	private const int HtTransparent = -1;

	private const TextFormatFlags TextFlags = TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;

	private OverlayPresentation _presentation;

	private OverlayThemePalette _palette = OverlayThemePalette.For(OverlayThemeKind.Dark);

	private OverlayEditGestureKind? _editGesture;

	private Point _gestureStartCursorScreen;

	private Rectangle _gestureStartBounds;

	private Point _fixedTopLeft;

	private int _gestureStartScalePercent = 100;

	private OverlayEditPreviewEventArgs? _lastEditPreview;

	public OverlayLayoutResult? CurrentLayout { get; private set; }

	public bool IsEditMode { get; private set; }

	internal OverlayPresentation CurrentPresentation => _presentation;

	internal OverlayThemePalette CurrentThemePalette => _palette;

	internal int SetBoundsCoreCallCount { get; private set; }

	internal bool IsEditGestureActive
	{
		get
		{
			OverlayEditGestureKind? editGesture = _editGesture;
			return editGesture.HasValue;
		}
	}

	internal OverlayRenderDecorationState RenderDecorations
	{
		get
		{
			if (!IsEditMode || (object)CurrentLayout == null || CurrentLayout.CapsuleBounds.IsEmpty)
			{
				return new OverlayRenderDecorationState(ShowBorder: false, ShowDragHint: false, ShowResizeHandle: false, string.Empty, default(IntRect), 0.0);
			}
			OverlayRenderMetrics overlayRenderMetrics = OverlayRenderMetrics.Create(CurrentLayout.Dpi, CurrentLayout.ScalePercent);
			Rectangle rectangle = Rectangle.Inflate(CurrentLayout.CapsuleBounds.ToRectangle(), -overlayRenderMetrics.HorizontalPadding, 0);
			int num = Math.Max(rectangle.Left, rectangle.Right - overlayRenderMetrics.EditHandleSize - overlayRenderMetrics.MetricGap);
			return new OverlayRenderDecorationState(ShowBorder: true, ShowDragHint: true, ShowResizeHandle: true, "拖动调整位置", new IntRect(rectangle.Left, rectangle.Top, num - rectangle.Left, rectangle.Height), overlayRenderMetrics.LabelFontPoints);
		}
	}

	internal Rectangle EditResizeHandleBounds
	{
		get
		{
			if ((object)CurrentLayout == null || CurrentLayout.CapsuleBounds.IsEmpty)
			{
				return Rectangle.Empty;
			}
			OverlayRenderMetrics overlayRenderMetrics = OverlayRenderMetrics.Create(CurrentLayout.Dpi, CurrentLayout.ScalePercent);
			Rectangle rectangle = CurrentLayout.CapsuleBounds.ToRectangle();
			int num = Math.Min(overlayRenderMetrics.EditHandleSize, Math.Min(rectangle.Width, rectangle.Height));
			return new Rectangle(rectangle.Right - num, rectangle.Bottom - num, num, num);
		}
	}

	protected override bool ShowWithoutActivation => !IsEditMode;

	protected override CreateParams CreateParams
	{
		get
		{
			CreateParams createParams = base.CreateParams;
			createParams.ExStyle |= 128;
			if (!IsEditMode)
			{
				createParams.ExStyle |= 134217728;
			}
			createParams.ClassStyle |= 131072;
			return createParams;
		}
	}

	public event EventHandler? CapsuleClicked;

	public event EventHandler<OverlayEditPreviewEventArgs>? EditPreviewChanged;

	public event EventHandler<OverlayEditPreviewEventArgs>? EditGestureCompleted;

	public event EventHandler? EditSaveRequested;

	public event EventHandler? EditCancelRequested;

	public TokenStripForm()
	{
		base.FormBorderStyle = FormBorderStyle.None;
		base.ShowInTaskbar = false;
		base.TopMost = true;
		base.StartPosition = FormStartPosition.Manual;
		base.AutoScaleMode = AutoScaleMode.None;
		BackColor = _palette.Background;
		ForeColor = _palette.Value;
		base.Opacity = 0.97;
		DoubleBuffered = true;
		_presentation = OverlayPresentationBuilder.CreateWaiting(string.Empty, DisplayField.Total, DisplayField.ContextPercent, DisplayField.Total | DisplayField.ContextPercent);
		ApplyLayout(new OverlayLayoutResult(OverlayVisualState.Collapsed, CollapsedDisplayMode.TwoFields, ExpansionDirection.Down, 96u, new IntRect(0, 0, 196, 34), new IntRect(0, 0, 196, 34), default(IntRect), 0));
	}

	public void SetPresentation(OverlayPresentation presentation)
	{
		ArgumentNullException.ThrowIfNull(presentation, "presentation");
		_presentation = presentation;
		Invalidate();
	}

	public void ApplyTheme(OverlayThemePalette palette)
	{
		ArgumentNullException.ThrowIfNull(palette, "palette");
		if (!(_palette == palette))
		{
			_palette = palette;
			BackColor = (IsHorizontalStripLayout() ? ResolveHorizontalStripBackground(palette) : palette.Background);
			ForeColor = palette.Value;
			Invalidate();
		}
	}

	public void ApplyLayout(OverlayLayoutResult layout)
	{
		ArgumentNullException.ThrowIfNull(layout, "layout");
		CurrentLayout = layout;
		bool flag = IsHorizontalStripLayout();
		BackColor = (flag ? ResolveHorizontalStripBackground(_palette) : _palette.Background);
		base.Opacity = (flag ? 0.82 : 0.97);
		SetBounds(layout.WindowBounds.X, layout.WindowBounds.Y, layout.WindowBounds.Width, layout.WindowBounds.Height, BoundsSpecified.All);
		OverlayRenderMetrics overlayRenderMetrics = OverlayRenderMetrics.Create(layout.Dpi, layout.ScalePercent);
		using GraphicsPath graphicsPath = new GraphicsPath();
		if (!layout.CapsuleBounds.IsEmpty)
		{
			if (layout.CollapsedDisplay == CollapsedDisplayMode.HorizontalStrip)
			{
				graphicsPath.AddRectangle(layout.CapsuleBounds.ToRectangle());
			}
			else
			{
				using GraphicsPath addingPath = CreateRoundedRectanglePath(layout.CapsuleBounds.ToRectangle(), overlayRenderMetrics.CapsuleRadius);
				graphicsPath.AddPath(addingPath, connect: false);
			}
		}
		if (!layout.PanelBounds.IsEmpty)
		{
			using GraphicsPath addingPath2 = CreateRoundedRectanglePath(layout.PanelBounds.ToRectangle(), overlayRenderMetrics.PanelRadius);
			graphicsPath.AddPath(addingPath2, connect: false);
		}
		base.Region?.Dispose();
		base.Region = new Region(graphicsPath);
		Invalidate();
	}

	public void BeginEditMode(int scalePercent)
	{
		if (!IsEditMode)
		{
			OverlayLayoutResult? currentLayout = CurrentLayout;
			if ((object)currentLayout == null || currentLayout.State != OverlayVisualState.Collapsed || !CurrentLayout.PanelBounds.IsEmpty)
			{
				throw new InvalidOperationException("编辑模式只能从收起布局开始。");
			}
			_gestureStartScalePercent = ManualAttachmentRules.SanitizeScale(scalePercent);
			IsEditMode = true;
			if (base.IsHandleCreated)
			{
				RecreateHandle();
			}
			Activate();
			Focus();
			Invalidate();
		}
	}

	public void EndEditMode()
	{
		if (IsEditMode)
		{
			CancelEditGesture();
			IsEditMode = false;
			if (base.IsHandleCreated)
			{
				RecreateHandle();
			}
			Invalidate();
		}
	}

	internal void SimulateCapsuleClick(Point screenPoint)
	{
		HandleMouseUp(MouseButtons.Left, PointToClient(screenPoint), screenPoint);
	}

	internal void SimulateEditDrag(Point startScreen, Point currentScreen)
	{
		HandleMouseDown(MouseButtons.Left, PointToClient(startScreen), startScreen);
		HandleMouseMove(PointToClient(currentScreen), currentScreen);
	}

	internal void SimulateEditResize(Point startScreen, Point currentScreen)
	{
		HandleMouseDown(MouseButtons.Left, PointToClient(startScreen), startScreen);
		HandleMouseMove(PointToClient(currentScreen), currentScreen);
	}

	internal void SimulateEditGestureCompleted(Point currentScreen)
	{
		HandleMouseUp(MouseButtons.Left, PointToClient(currentScreen), currentScreen);
	}

	internal void SimulateEditCaptureLost()
	{
		base.Capture = false;
		OverlayEditGestureKind? editGesture = _editGesture;
		if (editGesture.HasValue)
		{
			OnMouseCaptureChanged(EventArgs.Empty);
		}
	}

	internal bool SimulateEditCommand(Keys keyData)
	{
		return HandleEditCommand(keyData);
	}

	public bool ContainsScreenPoint(Point screenPoint)
	{
		return CurrentLayout?.ContainsScreenPoint(screenPoint) ?? false;
	}

	protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
	{
		SetBoundsCoreCallCount++;
		base.SetBoundsCore(x, y, width, height, specified);
	}

	protected override void WndProc(ref Message message)
	{
		if (message.Msg == 33 && !IsEditMode)
		{
			message.Result = 3;
			return;
		}
		if (message.Msg == 132 && (object)CurrentLayout != null)
		{
			long num = ((IntPtr)message.LParam).ToInt64();
			Point p = new Point((short)(num & 0xFFFF), (short)((num >> 16) & 0xFFFF));
			Point point = PointToClient(p);
			if (!CurrentLayout.CapsuleBounds.Contains(point.X, point.Y) && !CurrentLayout.PanelBounds.Contains(point.X, point.Y))
			{
				message.Result = -1;
				return;
			}
		}
		base.WndProc(ref message);
	}

	protected override void OnMouseDown(MouseEventArgs eventArgs)
	{
		base.OnMouseDown(eventArgs);
		HandleMouseDown(eventArgs.Button, eventArgs.Location, PointToScreen(eventArgs.Location));
	}

	protected override void OnMouseMove(MouseEventArgs eventArgs)
	{
		base.OnMouseMove(eventArgs);
		HandleMouseMove(eventArgs.Location, PointToScreen(eventArgs.Location));
	}

	protected override void OnMouseUp(MouseEventArgs eventArgs)
	{
		base.OnMouseUp(eventArgs);
		HandleMouseUp(eventArgs.Button, eventArgs.Location, PointToScreen(eventArgs.Location));
	}

	protected override void OnMouseCaptureChanged(EventArgs eventArgs)
	{
		base.OnMouseCaptureChanged(eventArgs);
		if (base.Capture)
		{
			return;
		}
		OverlayEditGestureKind? editGesture = _editGesture;
		if (editGesture.HasValue)
		{
			if ((object)_lastEditPreview != null)
			{
				CompleteEditGesture(_lastEditPreview, releaseCapture: false);
			}
			else
			{
				CancelEditGesture(releaseCapture: false);
			}
		}
	}

	protected override bool ProcessCmdKey(ref Message message, Keys keyData)
	{
		if (IsEditMode && HandleEditCommand(keyData))
		{
			return true;
		}
		return base.ProcessCmdKey(ref message, keyData);
	}

	private void HandleMouseDown(MouseButtons button, Point clientPoint, Point cursorScreen)
	{
		if (IsEditMode && button == MouseButtons.Left && (object)CurrentLayout != null && CurrentLayout.CapsuleBounds.Contains(clientPoint.X, clientPoint.Y))
		{
			_editGesture = (EditResizeHandleBounds.Contains(clientPoint) ? OverlayEditGestureKind.Resize : OverlayEditGestureKind.Move);
			_gestureStartCursorScreen = cursorScreen;
			_gestureStartBounds = base.Bounds;
			_fixedTopLeft = base.Location;
			_gestureStartScalePercent = ManualAttachmentRules.SanitizeScale(CurrentLayout.ScalePercent);
			_lastEditPreview = null;
			base.Capture = true;
		}
	}

	private void HandleMouseMove(Point clientPoint, Point cursorScreen)
	{
		if (!IsEditMode)
		{
			return;
		}
		OverlayEditGestureKind? editGesture = _editGesture;
		if (editGesture.HasValue)
		{
			int num = cursorScreen.X - _gestureStartCursorScreen.X;
			int num2 = cursorScreen.Y - _gestureStartCursorScreen.Y;
			int scalePercent = _gestureStartScalePercent;
			if (_editGesture == OverlayEditGestureKind.Move)
			{
				base.Location = new Point(_gestureStartBounds.X + num, _gestureStartBounds.Y + num2);
			}
			else
			{
				scalePercent = ManualAttachmentCalculator.CalculateScale(_gestureStartBounds.Size, _gestureStartScalePercent, num, num2);
			}
			_lastEditPreview = new OverlayEditPreviewEventArgs(_editGesture.Value, cursorScreen, _fixedTopLeft, scalePercent);
			EditPreviewChanged?.Invoke(this, _lastEditPreview);
		}
	}

	private void HandleMouseUp(MouseButtons button, Point clientPoint, Point cursorScreen)
	{
		if (button != MouseButtons.Left)
		{
			return;
		}
		if (IsEditMode)
		{
			OverlayEditGestureKind? editGesture = _editGesture;
			if (editGesture.HasValue)
			{
				HandleMouseMove(clientPoint, cursorScreen);
				OverlayEditPreviewEventArgs completed = _lastEditPreview ?? new OverlayEditPreviewEventArgs(_editGesture.Value, cursorScreen, _fixedTopLeft, _gestureStartScalePercent);
				CompleteEditGesture(completed, releaseCapture: true);
			}
		}
		else if ((object)CurrentLayout != null && CurrentLayout.CapsuleBounds.Contains(clientPoint.X, clientPoint.Y))
		{
			CapsuleClicked?.Invoke(this, EventArgs.Empty);
		}
	}

	private void CompleteEditGesture(OverlayEditPreviewEventArgs completed, bool releaseCapture)
	{
		_editGesture = null;
		_lastEditPreview = null;
		if (releaseCapture && base.Capture)
		{
			base.Capture = false;
		}
		EditGestureCompleted?.Invoke(this, completed);
	}

	private void CancelEditGesture(bool releaseCapture = true)
	{
		_editGesture = null;
		_lastEditPreview = null;
		if (releaseCapture && base.Capture)
		{
			base.Capture = false;
		}
	}

	private bool HandleEditCommand(Keys keyData)
	{
		if (!IsEditMode)
		{
			return false;
		}
		switch (keyData & Keys.KeyCode)
		{
		case Keys.Return:
			EditSaveRequested?.Invoke(this, EventArgs.Empty);
			return true;
		case Keys.Escape:
			EditCancelRequested?.Invoke(this, EventArgs.Empty);
			return true;
		default:
			return false;
		}
	}

	protected override void OnPaint(PaintEventArgs eventArgs)
	{
		base.OnPaint(eventArgs);
		if ((object)CurrentLayout == null)
		{
			return;
		}
		eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
		OverlayRenderMetrics metrics = OverlayRenderMetrics.Create(CurrentLayout.Dpi, CurrentLayout.ScalePercent);
		OverlayRenderDecorationState renderDecorations = RenderDecorations;
		using Font labelFont = new Font("Segoe UI", (float)(renderDecorations.ShowDragHint ? renderDecorations.DragHintFontPoints : metrics.LabelFontPoints), FontStyle.Regular, GraphicsUnit.Point);
		using Font valueFont = new Font("Segoe UI Semibold", (float)metrics.CompactValueFontPoints, FontStyle.Regular, GraphicsUnit.Point);
		using Font headerFont = new Font("Segoe UI Semibold", (float)metrics.PanelHeaderFontPoints, FontStyle.Regular, GraphicsUnit.Point);
		using Font highlightedValueFont = new Font("Segoe UI Semibold", (float)metrics.HighlightedValueFontPoints, FontStyle.Regular, GraphicsUnit.Point);
		using SolidBrush backgroundBrush = new SolidBrush(_palette.Background);
		using Pen borderPen = new Pen(_palette.Border, metrics.StrokeWidth);
		using Pen dividerPen = new Pen(_palette.Divider, metrics.StrokeWidth);
		using SolidBrush progressTrackBrush = new SolidBrush(_palette.ProgressTrack);
		DrawCapsule(eventArgs.Graphics, labelFont, valueFont, backgroundBrush, borderPen, dividerPen, metrics, renderDecorations);
		if (!CurrentLayout.PanelBounds.IsEmpty)
		{
			DrawPanel(eventArgs.Graphics, labelFont, valueFont, headerFont, highlightedValueFont, backgroundBrush, borderPen, dividerPen, progressTrackBrush, metrics, renderDecorations);
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			CancelEditGesture();
			base.Region?.Dispose();
			base.Region = null;
		}
		base.Dispose(disposing);
	}

	private void DrawCapsule(Graphics graphics, Font labelFont, Font valueFont, Brush backgroundBrush, Pen borderPen, Pen dividerPen, OverlayRenderMetrics metrics, OverlayRenderDecorationState decorations)
	{
		OverlayLayoutResult currentLayout = CurrentLayout;
		Rectangle rectangle = currentLayout.CapsuleBounds.ToRectangle();
		if (currentLayout.CollapsedDisplay == CollapsedDisplayMode.HorizontalStrip)
		{
			using (SolidBrush brush = new SolidBrush(ResolveHorizontalStripBackground(_palette)))
			{
				graphics.FillRectangle(brush, rectangle);
				Rectangle bounds = Rectangle.Inflate(rectangle, -metrics.HorizontalPadding, 0);
				TextRenderer.DrawText(graphics, BuildHorizontalStripText(_presentation), labelFont, bounds, ResolveHorizontalStripLabel(_palette), TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
				return;
			}
		}
		using GraphicsPath path = CreateRoundedRectanglePath(rectangle, metrics.CapsuleRadius);
		graphics.FillPath(backgroundBrush, path);
		if (decorations.ShowBorder)
		{
			graphics.DrawPath(borderPen, path);
		}
		if (decorations.ShowDragHint)
		{
			TextRenderer.DrawText(graphics, decorations.DragHintText, labelFont, decorations.DragHintBounds.ToRectangle(), _palette.Label, TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
			DrawEditHandle(graphics, dividerPen, metrics, decorations);
			return;
		}
		int horizontalPadding = metrics.HorizontalPadding;
		Rectangle bounds2 = Rectangle.Inflate(rectangle, -horizontalPadding, 0);
		if (!string.IsNullOrWhiteSpace(_presentation.StatusText))
		{
			TextRenderer.DrawText(graphics, _presentation.StatusText, labelFont, bounds2, _palette.Label, TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
			DrawEditHandle(graphics, dividerPen, metrics, decorations);
			return;
		}
		if (currentLayout.CollapsedDisplay == CollapsedDisplayMode.PrimaryOnly)
		{
			DrawCompactMetric(graphics, _presentation.Primary, bounds2, labelFont, valueFont, metrics);
			DrawEditHandle(graphics, dividerPen, metrics, decorations);
			return;
		}
		int metricGap = metrics.MetricGap;
		int num = bounds2.Left + bounds2.Width / 2;
		int num2 = Math.Min(metrics.DividerHeight, bounds2.Height);
		int num3 = bounds2.Top + (bounds2.Height - num2) / 2;
		graphics.DrawLine(dividerPen, num, num3, num, num3 + num2);
		Rectangle bounds3 = Rectangle.FromLTRB(bounds2.Left, bounds2.Top, num - metricGap, bounds2.Bottom);
		Rectangle bounds4 = Rectangle.FromLTRB(num + metricGap, bounds2.Top, bounds2.Right, bounds2.Bottom);
		DrawCompactMetric(graphics, _presentation.Primary, bounds3, labelFont, valueFont, metrics);
		DrawCompactMetric(graphics, _presentation.Secondary, bounds4, labelFont, valueFont, metrics);
		DrawEditHandle(graphics, dividerPen, metrics, decorations);
	}

	private void DrawEditHandle(Graphics graphics, Pen pen, OverlayRenderMetrics metrics, OverlayRenderDecorationState decorations)
	{
		if (decorations.ShowResizeHandle && !EditResizeHandleBounds.IsEmpty)
		{
			Rectangle editResizeHandleBounds = EditResizeHandleBounds;
			int strokeWidth = metrics.StrokeWidth;
			int num = Math.Max(strokeWidth, editResizeHandleBounds.Width / 2);
			graphics.DrawLine(pen, editResizeHandleBounds.Right - num, editResizeHandleBounds.Bottom - strokeWidth, editResizeHandleBounds.Right - strokeWidth, editResizeHandleBounds.Bottom - num);
			graphics.DrawLine(pen, editResizeHandleBounds.Right - Math.Max(strokeWidth, editResizeHandleBounds.Width / 3), editResizeHandleBounds.Bottom - strokeWidth, editResizeHandleBounds.Right - strokeWidth, editResizeHandleBounds.Bottom - Math.Max(strokeWidth, editResizeHandleBounds.Height / 3));
		}
	}

	internal static string BuildHorizontalStripText(OverlayPresentation presentation)
	{
		ArgumentNullException.ThrowIfNull(presentation, "presentation");
		if (!string.IsNullOrWhiteSpace(presentation.StatusText))
		{
			return presentation.StatusText;
		}
		Dictionary<DisplayField, string> metrics = (from metric in new OverlayMetric[2] { presentation.Primary, presentation.Secondary }.Concat(presentation.ExpandedRows)
			group metric by metric.Field).ToDictionary((IGrouping<DisplayField, OverlayMetric> group) => group.Key, (IGrouping<DisplayField, OverlayMetric> group) => group.First().Value);
		InlineArray6<string> buffer = default(InlineArray6<string>);
		buffer[0] = "输入 " + Value(DisplayField.Input);
		buffer[1] = "输出 " + Value(DisplayField.Output);
		buffer[2] = "缓存命中 " + Value(DisplayField.CacheHit);
		buffer[3] = "缓存 " + Value(DisplayField.CacheHitRate);
		buffer[4] = "上下文 " + Value(DisplayField.ContextPercent);
		buffer[5] = "约 " + Value(DisplayField.OutputRate);
		return string.Join(" | ", (ReadOnlySpan<string?>)buffer);
		string Value(DisplayField field)
		{
			if (!metrics.TryGetValue(field, out var value))
			{
				return "—";
			}
			return value;
		}
	}

	private bool IsHorizontalStripLayout()
	{
		OverlayLayoutResult? currentLayout = CurrentLayout;
		if ((object)currentLayout == null)
		{
			return false;
		}
		return currentLayout.CollapsedDisplay == CollapsedDisplayMode.HorizontalStrip;
	}

	private static Color ResolveHorizontalStripBackground(OverlayThemePalette palette)
	{
		if (!(palette.Background.GetBrightness() >= 0.5f))
		{
			return Color.FromArgb(24, 24, 24);
		}
		return Color.White;
	}

	private static Color ResolveHorizontalStripLabel(OverlayThemePalette palette)
	{
		if (!(palette.Background.GetBrightness() >= 0.5f))
		{
			return palette.Label;
		}
		return Color.FromArgb(151, 154, 160);
	}

	private void DrawPanel(Graphics graphics, Font labelFont, Font valueFont, Font headerFont, Font highlightedValueFont, Brush backgroundBrush, Pen borderPen, Pen dividerPen, Brush progressTrackBrush, OverlayRenderMetrics metrics, OverlayRenderDecorationState decorations)
	{
		OverlayLayoutResult currentLayout = CurrentLayout;
		Rectangle rectangle = currentLayout.PanelBounds.ToRectangle();
		using GraphicsPath path = CreateRoundedRectanglePath(rectangle, metrics.PanelRadius);
		graphics.FillPath(backgroundBrush, path);
		if (decorations.ShowBorder)
		{
			graphics.DrawPath(borderPen, path);
		}
		int panelPadding = metrics.PanelPadding;
		Rectangle rectangle2 = Rectangle.Inflate(rectangle, -panelPadding, -panelPadding);
		int headerHeight = metrics.HeaderHeight;
		TextRenderer.DrawText(graphics, "Token 详情", headerFont, new Rectangle(rectangle2.Left, rectangle2.Top, rectangle2.Width, headerHeight), _palette.Value, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
		int num = rectangle2.Top + headerHeight + metrics.HighlightTopGap;
		int highlightHeight = metrics.HighlightHeight;
		int metricGap = metrics.MetricGap;
		int num2 = Math.Max(0, (rectangle2.Width - metricGap) / 2);
		DrawHighlightedMetric(graphics, _presentation.Primary, new Rectangle(rectangle2.Left, num, num2, highlightHeight), labelFont, highlightedValueFont);
		DrawHighlightedMetric(graphics, _presentation.Secondary, new Rectangle(rectangle2.Left + num2 + metricGap, num, num2, highlightHeight), labelFont, highlightedValueFont);
		int num3 = ((currentLayout.ExpandedRowHeight > 0) ? (currentLayout.ExpandedRowHeight * _presentation.ExpandedRows.Count) : 0);
		int num4 = Math.Max(num + highlightHeight, rectangle.Bottom - panelPadding - num3);
		if (_presentation.ShowContextProgress)
		{
			int progressTrackHeight = metrics.ProgressTrackHeight;
			int y = Math.Min(num4 - progressTrackHeight - metrics.ProgressVerticalGap, num + highlightHeight + metrics.ProgressVerticalGap);
			Rectangle rect = new Rectangle(rectangle2.Left, y, rectangle2.Width, progressTrackHeight);
			graphics.FillRectangle(progressTrackBrush, rect);
			int num5 = (int)Math.Round((double)rect.Width * Math.Clamp(_presentation.ContextPercent, 0.0, 100.0) / 100.0, MidpointRounding.AwayFromZero);
			if (num5 > 0)
			{
				Rectangle rect2 = new Rectangle(rect.X, rect.Y, num5, rect.Height);
				using LinearGradientBrush brush = new LinearGradientBrush(rect2, _palette.ProgressStart, _palette.ProgressEnd, LinearGradientMode.Horizontal);
				graphics.FillRectangle(brush, rect2);
			}
		}
		for (int i = 0; i < _presentation.ExpandedRows.Count; i++)
		{
			OverlayMetric metric = _presentation.ExpandedRows[i];
			Rectangle bounds = new Rectangle(rectangle2.Left, num4 + i * currentLayout.ExpandedRowHeight, rectangle2.Width, currentLayout.ExpandedRowHeight);
			graphics.DrawLine(dividerPen, bounds.Left, bounds.Top, bounds.Right, bounds.Top);
			DrawExpandedRow(graphics, metric, bounds, labelFont, valueFont);
		}
	}

	private void DrawCompactMetric(Graphics graphics, OverlayMetric metric, Rectangle bounds, Font labelFont, Font valueFont, OverlayRenderMetrics metrics)
	{
		if (bounds.Width > 0 && bounds.Height > 0)
		{
			int width = TextRenderer.MeasureText(graphics, metric.CompactLabel, labelFont, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;
			int compactMetricGap = metrics.CompactMetricGap;
			width = Math.Min(width, bounds.Width);
			Rectangle bounds2 = new Rectangle(bounds.Left, bounds.Top, width, bounds.Height);
			Rectangle bounds3 = Rectangle.FromLTRB(Math.Min(bounds.Right, bounds2.Right + compactMetricGap), bounds.Top, bounds.Right, bounds.Bottom);
			TextRenderer.DrawText(graphics, metric.CompactLabel, labelFont, bounds2, _palette.Label, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
			TextRenderer.DrawText(graphics, metric.Value, valueFont, bounds3, ValueColorFor(metric), TextFormatFlags.EndEllipsis | TextFormatFlags.Right | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
		}
	}

	private void DrawHighlightedMetric(Graphics graphics, OverlayMetric metric, Rectangle bounds, Font labelFont, Font valueFont)
	{
		int num = Math.Max(1, bounds.Height / 2);
		TextRenderer.DrawText(graphics, metric.ExpandedLabel, labelFont, new Rectangle(bounds.Left, bounds.Top, bounds.Width, num), _palette.Label, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
		TextRenderer.DrawText(graphics, metric.Value, valueFont, new Rectangle(bounds.Left, bounds.Top + num, bounds.Width, bounds.Height - num), ValueColorFor(metric), TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
	}

	private void DrawExpandedRow(Graphics graphics, OverlayMetric metric, Rectangle bounds, Font labelFont, Font valueFont)
	{
		int num = Math.Max(0, bounds.Width / 2);
		TextRenderer.DrawText(graphics, metric.ExpandedLabel, labelFont, new Rectangle(bounds.Left, bounds.Top, num, bounds.Height), _palette.Label, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
		TextRenderer.DrawText(graphics, metric.Value, valueFont, new Rectangle(bounds.Left + num, bounds.Top, bounds.Width - num, bounds.Height), ValueColorFor(metric), TextFormatFlags.EndEllipsis | TextFormatFlags.Right | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
	}

	private Color ValueColorFor(OverlayMetric metric)
	{
		DisplayField field = metric.Field;
		if ((field != DisplayField.Context && field != DisplayField.ContextPercent) || 1 == 0)
		{
			return _palette.Value;
		}
		return _palette.Accent;
	}

	private static GraphicsPath CreateRoundedRectanglePath(Rectangle rectangle, int radius)
	{
		GraphicsPath graphicsPath = new GraphicsPath();
		if (rectangle.Width <= 0 || rectangle.Height <= 0)
		{
			return graphicsPath;
		}
		int num = Math.Clamp(radius, 0, Math.Min(rectangle.Width, rectangle.Height) / 2);
		if (num == 0)
		{
			graphicsPath.AddRectangle(rectangle);
			return graphicsPath;
		}
		int num2 = num * 2;
		graphicsPath.AddArc(rectangle.Left, rectangle.Top, num2, num2, 180f, 90f);
		graphicsPath.AddArc(rectangle.Right - num2, rectangle.Top, num2, num2, 270f, 90f);
		graphicsPath.AddArc(rectangle.Right - num2, rectangle.Bottom - num2, num2, num2, 0f, 90f);
		graphicsPath.AddArc(rectangle.Left, rectangle.Bottom - num2, num2, num2, 90f, 90f);
		graphicsPath.CloseFigure();
		return graphicsPath;
	}
}
