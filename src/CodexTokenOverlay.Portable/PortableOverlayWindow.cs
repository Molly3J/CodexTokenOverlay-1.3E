using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace CodexTokenOverlay;

internal sealed class PortableOverlayWindow : Window
{
    private static readonly IBrush PanelBrush = Brush.Parse("#F2171A21");
    private static readonly IBrush FrameBorderBrush = Brush.Parse("#3E596579");
    private static readonly IBrush PrimaryBrush = Brush.Parse("#F5F7FA");
    private static readonly IBrush SecondaryBrush = Brush.Parse("#9EABB9");
    private static readonly IBrush AccentBrush = Brush.Parse("#6FA8FF");

    private readonly TokenLogMonitor _monitor;
    private readonly DispatcherTimer _timer;
    private readonly TextBlock _title;
    private readonly TextBlock _status;
    private readonly TextBlock _total;
    private readonly TextBlock _input;
    private readonly TextBlock _output;
    private readonly TextBlock _cache;
    private readonly TextBlock _context;
    private readonly TextBlock _rate;
    private readonly ProgressBar _contextProgress;

    public PortableOverlayWindow(string sessionRoot)
    {
        _monitor = new TokenLogMonitor(sessionRoot, allowNonDesktopSessions: true);

        Title = "Codex Token Overlay";
        Width = 860;
        Height = 96;
        MinWidth = 700;
        CanResize = true;
        Topmost = true;
        ShowInTaskbar = true;
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brushes.Transparent;

        _title = CreateTitle("CODEX");
        _status = new TextBlock
        {
            Text = "正在等待 Codex 会话…",
            Foreground = SecondaryBrush,
            FontSize = 11,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 190
        };
        _total = CreateMetricValue();
        _input = CreateMetricValue();
        _output = CreateMetricValue();
        _cache = CreateMetricValue();
        _context = CreateMetricValue();
        _rate = CreateMetricValue();
        _contextProgress = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Height = 3,
            Margin = new Thickness(0, 5, 0, 0),
            Foreground = AccentBrush,
            Background = Brush.Parse("#25303C")
        };

        Border frame = new()
        {
            Background = PanelBrush,
            BorderBrush = FrameBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(13),
            Padding = new Thickness(15, 11),
            Child = BuildContent()
        };
        frame.PointerPressed += OnFramePointerPressed;
        Content = frame;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => RefreshSnapshot();
        _timer.Start();
        Closed += (_, _) =>
        {
            _timer.Stop();
            _monitor.Dispose();
        };

        RefreshSnapshot();
    }

    private Control BuildContent()
    {
        Grid grid = new()
        {
            ColumnDefinitions = new ColumnDefinitions("200,*,*,*,*,*,*,34"),
            RowDefinitions = new RowDefinitions("*")
        };

        StackPanel identity = new() { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        identity.Children.Add(_title);
        identity.Children.Add(_status);
        AddAt(grid, identity, 0);

        AddAt(grid, CreateMetric("总 TOKEN", _total), 1);
        AddAt(grid, CreateMetric("输入", _input), 2);
        AddAt(grid, CreateMetric("输出", _output), 3);
        AddAt(grid, CreateMetric("缓存命中", _cache), 4);

        StackPanel contextPanel = CreateMetric("上下文", _context);
        contextPanel.Children.Add(_contextProgress);
        AddAt(grid, contextPanel, 5);
        AddAt(grid, CreateMetric("墙钟速率", _rate), 6);

        Button close = new()
        {
            Content = "×",
            Width = 28,
            Height = 28,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            Foreground = SecondaryBrush,
            BorderThickness = new Thickness(0),
            FontSize = 19
        };
        ToolTip.SetTip(close, "退出");
        close.Click += (_, _) => Close();
        AddAt(grid, close, 7);
        return grid;
    }

    private void RefreshSnapshot()
    {
        try
        {
            TokenSnapshot? snapshot = _monitor.Poll();
            if (snapshot == null)
            {
                _status.Text = "正在等待 Codex 会话…";
                return;
            }

            _title.Text = string.IsNullOrWhiteSpace(snapshot.ModelId)
                ? "CODEX"
                : snapshot.ModelId.ToUpperInvariant();
            _status.Text = $"会话 {ShortThreadId(snapshot.ThreadId)} · {snapshot.UpdatedAtUtc.ToLocalTime():HH:mm:ss}";
            _total.Text = FormatCount(snapshot.TotalTokens);
            _input.Text = FormatCount(snapshot.InputTokens);
            _output.Text = FormatCount(snapshot.OutputTokens);
            _cache.Text = $"{FormatCount(snapshot.CachedInputTokens)}  {snapshot.CacheHitPercent:0}%";
            _context.Text = snapshot.ContextWindowTokens > 0
                ? $"{FormatCount(snapshot.ContextUsedTokens)} / {FormatCount(snapshot.ContextWindowTokens)}"
                : FormatCount(snapshot.ContextUsedTokens);
            _contextProgress.Value = snapshot.ContextPercent;
            _rate.Text = snapshot.OutputTokensPerSecond.HasValue
                ? $"{snapshot.OutputTokensPerSecond.Value:0.0} tok/s"
                : "—";
        }
        catch (Exception exception)
        {
            _status.Text = "读取会话失败；正在重试";
            OverlayDiagnostics.Write("portable overlay refresh failed", exception);
        }
    }

    private void OnFramePointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        PointerPoint point = eventArgs.GetCurrentPoint(this);
        if (point.Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(eventArgs);
        }
    }

    private static StackPanel CreateMetric(string label, TextBlock value)
    {
        StackPanel panel = new()
        {
            Spacing = 3,
            Margin = new Thickness(9, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = SecondaryBrush,
            FontSize = 10,
            FontWeight = FontWeight.SemiBold
        });
        panel.Children.Add(value);
        return panel;
    }

    private static TextBlock CreateMetricValue()
    {
        return new TextBlock
        {
            Text = "—",
            Foreground = PrimaryBrush,
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
    }

    private static TextBlock CreateTitle(string text)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = PrimaryBrush,
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 190
        };
    }

    private static void AddAt(Grid grid, Control control, int column)
    {
        Grid.SetColumn(control, column);
        grid.Children.Add(control);
    }

    private static string FormatCount(long value)
    {
        return value switch
        {
            >= 1_000_000 => $"{value / 1_000_000d:0.00}M",
            >= 1_000 => $"{value / 1_000d:0.0}k",
            _ => value.ToString("N0")
        };
    }

    private static string ShortThreadId(string value)
    {
        return value.Length <= 12 ? value : $"{value[..4]}…{value[^6..]}";
    }
}
