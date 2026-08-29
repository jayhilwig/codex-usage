using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using CodexUsage.Core.Reset;
using CodexUsage.Core.Ui;
using CodexUsage.Core.Window;
using CodexUsage.Desktop.Platform;

namespace CodexUsage.Desktop.Ui;

internal sealed class TitlebarHudWindow : Window
{
    private const double HudWidth = 174;
    private const double HudHeight = 30;
    private readonly HudViewModel _viewModel;
    private readonly ICodexWindowTracker _tracker;
    private readonly Border _usageButton;
    private readonly Border _resetButton;
    private readonly TextBlock _fiveHourText;
    private readonly TextBlock _separatorText;
    private readonly TextBlock _weeklyText;
    private readonly TextBlock _resetText;
    private readonly UsagePopoverWindow _usagePopover;
    private readonly ResetPopoverWindow _resetPopover;
    private readonly DispatcherTimer _trackerTimer;
    private readonly List<Action> _updateButtonHighlights = [];
    private CodexWindowSnapshot? _target;
    private bool _dark = true;

    public TitlebarHudWindow(
        HudViewModel viewModel,
        ICodexWindowTracker tracker,
        Action shutdown)
    {
        _viewModel = viewModel;
        _tracker = tracker;
        Width = HudWidth;
        Height = HudHeight;
        MinWidth = HudWidth;
        MinHeight = HudHeight;
        MaxWidth = HudWidth;
        MaxHeight = HudHeight;
        CanResize = false;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = false;
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Background = Brushes.Transparent;
        TransparencyBackgroundFallback = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        ExtendClientAreaToDecorationsHint = true;
        UseLayoutRounding = true;

        _fiveHourText = MakeHudText("5h --");
        _separatorText = MakeHudText(" · ");
        _weeklyText = MakeHudText("W --");
        _resetText = MakeHudText("↺");
        _resetText.Margin = new Thickness(0, -1, 0, 0);

        var usageLine = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 0,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _fiveHourText, _separatorText, _weeklyText },
        };
        _usageButton = MakeHudButton(usageLine, new Thickness(5, 0, 4, 0), ToggleUsagePopover);
        _resetButton = MakeHudButton(_resetText, new Thickness(4, 0, 5, 0), ToggleResetPopover);
        ToolTip.SetTip(_usageButton, "Codex usage remaining");
        ToolTip.SetTip(_resetButton, "Latest Codex reset unavailable");

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 0,
            Children = { _usageButton, _resetButton },
        };

        var exit = new MenuItem { Header = "Exit Codex Usage" };
        exit.Click += (_, _) => shutdown();
        panel.ContextMenu = new ContextMenu { Items = { exit } };
        Content = panel;

        _usagePopover = new UsagePopoverWindow();
        _resetPopover = new ResetPopoverWindow();
        _viewModel.Changed += (_, _) => Dispatcher.UIThread.Post(ApplyViewModel);

        Opened += (_, _) => ConfigureNativeWindow();
        _trackerTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Normal, TrackTarget);
        _trackerTimer.Start();
        ApplyViewModel();
    }

    private static TextBlock MakeHudText(string text)
    {
        var textBlock = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11,
            FontWeight = FontWeight.Normal,
            LineHeight = 13,
            UseLayoutRounding = true,
            VerticalAlignment = VerticalAlignment.Center,
        };
        TextOptions.SetTextRenderingMode(textBlock, TextRenderingMode.SubpixelAntialias);
        TextOptions.SetTextHintingMode(textBlock, TextHintingMode.Strong);
        TextOptions.SetBaselinePixelAlignment(textBlock, BaselinePixelAlignment.Aligned);
        return textBlock;
    }

    private Border MakeHudButton(Control content, Thickness padding, Action onClick)
    {
        var highlight = new Border
        {
            Padding = padding,
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(4),
            VerticalAlignment = VerticalAlignment.Center,
            Child = content,
            MinWidth = 0,
            MinHeight = HudHeight,
            Height = HudHeight,
            Cursor = new Cursor(StandardCursorType.Hand),
            UseLayoutRounding = true,
        };

        var hovered = false;
        var pressed = false;
        void UpdateHighlight()
        {
            highlight.Background = pressed
                ? new SolidColorBrush(Color.Parse(_dark ? "#20FFFFFF" : "#17000000"))
                : hovered
                    ? new SolidColorBrush(Color.Parse(_dark ? "#14FFFFFF" : "#0D000000"))
                    : Brushes.Transparent;
        }

        highlight.PointerEntered += (_, _) =>
        {
            hovered = true;
            UpdateHighlight();
        };
        highlight.PointerExited += (_, _) =>
        {
            hovered = false;
            pressed = false;
            UpdateHighlight();
        };
        highlight.PointerPressed += (_, eventArgs) =>
        {
            pressed = eventArgs.GetCurrentPoint(highlight).Properties.IsLeftButtonPressed;
            UpdateHighlight();
        };
        highlight.PointerReleased += (_, eventArgs) =>
        {
            var activate = pressed
                && eventArgs.InitialPressMouseButton == MouseButton.Left
                && highlight.IsPointerOver;
            pressed = false;
            UpdateHighlight();
            if (activate)
            {
                onClick();
            }
        };
        highlight.PointerCaptureLost += (_, _) =>
        {
            pressed = false;
            UpdateHighlight();
        };
        _updateButtonHighlights.Add(UpdateHighlight);
        return highlight;
    }

    private void TrackTarget(object? sender, EventArgs args)
    {
        try
        {
            TrackTargetCore();
        }
        catch (Exception error)
        {
            System.Diagnostics.Debug.WriteLine($"HUD tracker failed: {error}");
            _usagePopover.Hide();
            _resetPopover.Hide();
            Hide();
        }
    }

    private void TrackTargetCore()
    {
        if (!_tracker.TryGetSnapshot(out var target)
            || target is null
            || !target.IsVisible
            || target.IsMinimized)
        {
            if (IsVisible)
            {
                _usagePopover.Hide();
                _resetPopover.Hide();
                Hide();
            }

            return;
        }

        _target = target;
        _dark = target.IsDarkMode;
        ApplyTheme();

        var scale = Math.Max(1, target.DisplayScale);
        var width = (int)Math.Round(HudWidth * scale);
        var height = (int)Math.Round(HudHeight * scale);
        var margin = (int)Math.Round(8 * scale);
        int x;
        int y;

        if (target.CaptionButtons is { } buttons && buttons.Width > 0 && buttons.Height > 0)
        {
            x = target.Bounds.Left + buttons.Left - margin - width;
            y = target.Bounds.Top + buttons.Top + Math.Max(0, (buttons.Height - height) / 2);
        }
        else
        {
            var nativeButtonGroupWidth = (int)Math.Round(138 * scale);
            x = target.Bounds.Right - nativeButtonGroupWidth - margin - width;
            y = target.Bounds.Top + (int)Math.Round(5 * scale);
        }

        x = Math.Max(target.Bounds.Left + margin, x);
        Position = new PixelPoint(x, y);
        if (!IsVisible)
        {
            Show();
            ConfigureNativeWindow();
        }

        var handle = TryGetPlatformHandle()?.Handle ?? nint.Zero;
        WindowsOverlayInterop.Position(handle, x, y, width, height);
        PositionPopovers(x, y, width, height, scale);
    }

    private void PositionPopovers(int x, int y, int width, int height, double scale)
    {
        var gap = (int)Math.Round(6 * scale);
        if (_usagePopover.IsVisible)
        {
            var popupWidth = (int)Math.Round(_usagePopover.Width * scale);
            _usagePopover.Position = new PixelPoint(x + width - popupWidth, y + height + gap);
        }

        if (_resetPopover.IsVisible)
        {
            var popupWidth = (int)Math.Round(_resetPopover.Width * scale);
            _resetPopover.Position = new PixelPoint(x + width - popupWidth, y + height + gap);
        }
    }

    private void ConfigureNativeWindow()
    {
        if (_target is null)
        {
            return;
        }

        var handle = TryGetPlatformHandle()?.Handle ?? nint.Zero;
        WindowsOverlayInterop.ConfigureHud(handle, _target.NativeHandle);
    }

    private void ToggleUsagePopover()
    {
        _resetPopover.Hide();
        if (_usagePopover.IsVisible)
        {
            _usagePopover.Hide();
            return;
        }

        _usagePopover.Update(_viewModel);
        _usagePopover.ShowFor(this);
    }

    private void ToggleResetPopover()
    {
        _usagePopover.Hide();
        if (_resetPopover.IsVisible)
        {
            _resetPopover.Hide();
            return;
        }

        _resetPopover.Update(_viewModel);
        _resetPopover.ShowFor(this);
    }

    private void ApplyViewModel()
    {
        _fiveHourText.Text = _viewModel.Usage?.FiveHour is { } five
            ? $"5h {five.RemainingPercent}%"
            : "5h --";
        _weeklyText.Text = _viewModel.Usage?.Weekly is { } weekly
            ? $"W {weekly.RemainingPercent}%"
            : "W --";
        ToolTip.SetTip(
            _resetButton,
            _viewModel.LatestReset is { } latest
                ? $"Last reset: {HudViewModel.FormatAge(latest.AnnouncedAt, DateTimeOffset.UtcNow)}"
                : "Latest Codex reset unavailable");
        _usagePopover.Update(_viewModel);
        _resetPopover.Update(_viewModel);
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        var text = Color.Parse(_dark ? "#A6ABAF" : "#7E8489");
        _separatorText.Foreground = new SolidColorBrush(text);
        _fiveHourText.Foreground = MakeQuotaBrush(
            QuotaStatusEvaluator.EvaluateFiveHour(_viewModel.Usage?.FiveHour),
            text);
        _weeklyText.Foreground = MakeQuotaBrush(
            QuotaStatusEvaluator.EvaluateWeekly(_viewModel.Usage?.Weekly, DateTimeOffset.UtcNow),
            text);
        var latestResetAge = _viewModel.LatestReset is { } latest
            ? DateTimeOffset.UtcNow - latest.AnnouncedAt
            : TimeSpan.MaxValue;
        var recentReset = _viewModel.LatestResetIsFresh
            && latestResetAge >= TimeSpan.Zero
            && latestResetAge < TimeSpan.FromHours(12);
        _resetText.Foreground = new SolidColorBrush(
            recentReset ? Color.Parse(_dark ? "#65BE85" : "#3F9360") : text);
        foreach (var updateHighlight in _updateButtonHighlights)
        {
            updateHighlight();
        }
    }

    private SolidColorBrush MakeQuotaBrush(QuotaStatusLevel status, Color normal) => new(
        status switch
        {
            QuotaStatusLevel.Amber => Color.Parse(_dark ? "#E0A34A" : "#B87518"),
            QuotaStatusLevel.Red => Color.Parse(_dark ? "#E96B6B" : "#C84848"),
            _ => normal,
        });
}
