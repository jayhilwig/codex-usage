using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace CodexUsage.Desktop.Ui;

internal abstract class CompanionPopoverWindow : Window
{
    private bool _dismissOnDeactivate;

    protected CompanionPopoverWindow(double width)
    {
        Width = width;
        MinWidth = width;
        MaxWidth = width;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        ShowInTaskbar = false;
        ShowActivated = true;
        Topmost = false;
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Background = Brushes.Transparent;
        TransparencyBackgroundFallback = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Deactivated += (_, _) =>
        {
            if (_dismissOnDeactivate)
            {
                Hide();
            }
        };
    }

    public void ShowFor(Window owner)
    {
        _dismissOnDeactivate = false;
        Show(owner);
        Dispatcher.UIThread.Post(() =>
        {
            Activate();
            _dismissOnDeactivate = true;
        }, DispatcherPriority.Background);
    }

    protected void SetCard(Control content)
    {
        Content = new Border
        {
            Margin = new Thickness(8),
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#E6E6E7")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(13, 11),
            BoxShadow = BoxShadows.Parse("0 6 18 0 #16000000, 0 1 3 0 #12000000"),
            Child = content,
        };
    }

    protected static TextBlock MakeTitle(string text) => new()
    {
        Text = text,
        FontFamily = FontFamily.Default,
        FontSize = 12.5,
        FontWeight = FontWeight.Medium,
        Foreground = new SolidColorBrush(Color.Parse("#6A6B6D")),
    };

    protected static TextBlock MakeBody(string text) => new()
    {
        Text = text,
        FontFamily = FontFamily.Default,
        FontSize = 12,
        Foreground = new SolidColorBrush(Color.Parse("#6A6B6D")),
        VerticalAlignment = VerticalAlignment.Center,
    };

    protected static TextBlock MakeSecondary(string text) => new()
    {
        Text = text,
        FontFamily = FontFamily.Default,
        FontSize = 11.5,
        Foreground = new SolidColorBrush(Color.Parse("#8F9091")),
        VerticalAlignment = VerticalAlignment.Center,
    };
}
