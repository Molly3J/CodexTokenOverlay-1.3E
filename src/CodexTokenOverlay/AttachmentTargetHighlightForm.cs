using System;
using System.Drawing;
using System.Windows.Forms;

namespace CodexTokenOverlay;

internal sealed class AttachmentTargetHighlightForm : Form
{
	private const int WsExTransparent = 32;

	private const int WsExToolWindow = 128;

	private const int WsExNoActivate = 134217728;

	private const int WmNcHitTest = 132;

	private const int HtTransparent = -1;

	private const int RingWidthDip = 2;

	private static readonly Color TransparencyColor = Color.Fuchsia;

	private OverlayThemePalette _palette = OverlayThemePalette.For(OverlayThemeKind.Dark);

	internal int SetBoundsCoreCallCount { get; private set; }

	internal OverlayThemePalette CurrentThemePalette => _palette;

	protected override bool ShowWithoutActivation => true;

	protected override CreateParams CreateParams
	{
		get
		{
			CreateParams createParams = base.CreateParams;
			createParams.ExStyle |= 134217888;
			return createParams;
		}
	}

	public AttachmentTargetHighlightForm()
	{
		base.FormBorderStyle = FormBorderStyle.None;
		base.ShowInTaskbar = false;
		base.TopMost = true;
		base.StartPosition = FormStartPosition.Manual;
		base.AutoScaleMode = AutoScaleMode.None;
		BackColor = TransparencyColor;
		base.TransparencyKey = TransparencyColor;
		DoubleBuffered = true;
	}

	public void ApplyTheme(OverlayThemePalette palette)
	{
		ArgumentNullException.ThrowIfNull(palette, "palette");
		if (!(_palette == palette))
		{
			_palette = palette;
			Invalidate();
		}
	}

	public void ShowTarget(IntRect bounds)
	{
		if (bounds.IsEmpty)
		{
			ClearTarget();
			return;
		}
		SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height, BoundsSpecified.All);
		ReplaceRegion(CreateRingRegion(base.ClientSize, base.DeviceDpi));
		if (!base.Visible)
		{
			Show();
		}
		Invalidate();
	}

	public void ClearTarget()
	{
		if (base.Visible)
		{
			Hide();
		}
		ReplaceRegion(null);
	}

	protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
	{
		SetBoundsCoreCallCount++;
		base.SetBoundsCore(x, y, width, height, specified);
	}

	protected override void WndProc(ref Message message)
	{
		if (message.Msg == 132)
		{
			message.Result = -1;
		}
		else
		{
			base.WndProc(ref message);
		}
	}

	protected override void OnPaint(PaintEventArgs eventArgs)
	{
		base.OnPaint(eventArgs);
		eventArgs.Graphics.Clear(_palette.TargetHighlight);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			ReplaceRegion(null);
		}
		base.Dispose(disposing);
	}

	private void ReplaceRegion(Region? next)
	{
		Region? region = base.Region;
		base.Region = next;
		region?.Dispose();
	}

	private static Region CreateRingRegion(Size size, int dpi)
	{
		Rectangle rect = new Rectangle(Point.Empty, size);
		Region region = new Region(rect);
		int num = ((dpi <= 0) ? 96 : dpi);
		int num2 = Math.Max(1, (int)Math.Round((double)(2 * num) / 96.0, MidpointRounding.AwayFromZero));
		if (size.Width > num2 * 2 && size.Height > num2 * 2)
		{
			region.Exclude(Rectangle.Inflate(rect, -num2, -num2));
		}
		return region;
	}
}
