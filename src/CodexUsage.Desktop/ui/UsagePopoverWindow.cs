using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using CodexUsage.Core.Ui;

namespace CodexUsage.Desktop.Ui;

internal sealed class UsagePopoverWindow : CompanionPopoverWindow
{
    public UsagePopoverWindow() : base(248)
    {
    }

    public void Update(HudViewModel viewModel)
    {
        var now = DateTimeOffset.UtcNow;
        var content = new StackPanel { Spacing = 7 };
        content.Children.Add(MakeTitle("Usage remaining"));

        if (viewModel.Usage is { } usage
            && (usage.FiveHour is not null || usage.Weekly is not null))
        {
            content.Children.Add(MakeUsageRow("5h", usage.FiveHour, now));
            content.Children.Add(MakeUsageRow("Weekly", usage.Weekly, now));
        }
        else
        {
            content.Children.Add(MakeBody("Codex usage is temporarily unavailable."));
        }

        if (viewModel.Usage?.Credits is { } credits)
        {
            content.Children.Add(new Border
            {
                Height = 1,
                Margin = new Thickness(0, 3, 0, 1),
                Background = new SolidColorBrush(Color.Parse("#ECECEE")),
            });
            content.Children.Add(MakeCreditsRow(credits));
        }

        SetCard(content);
    }

    private static Border MakeCreditsRow(CodexUsage.Core.Usage.CreditBalance credits)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var label = MakeSecondary("Credits");
        label.Foreground = new SolidColorBrush(Color.Parse("#339cff"));
        var roundedBalance = Math.Round(credits.Balance, 0, MidpointRounding.AwayFromZero);
        var balance = MakeSecondary($"{roundedBalance:0} credits");
        balance.FontWeight = FontWeight.Medium;
        Grid.SetColumn(balance, 1);
        grid.Children.Add(label);
        grid.Children.Add(balance);

        var row = new Border
        {
            Padding = new Thickness(0, 2),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = grid,
        };
        row.PointerReleased += (_, eventArgs) =>
        {
            if (eventArgs.InitialPressMouseButton == MouseButton.Left && row.IsPointerOver)
            {
                OpenCreditsDashboard();
            }
        };
        return row;
    }

    private static Grid MakeUsageRow(string label, CodexUsage.Core.Usage.UsageWindow? window, DateTimeOffset now)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("54,44,*") };
        var labelText = MakeBody(label);
        var percentText = MakeBody(window is null ? "--" : $"{window.RemainingPercent}%");
        percentText.FontWeight = FontWeight.Medium;
        var resetText = MakeSecondary(HudViewModel.FormatUsageReset(window, now));
        Grid.SetColumn(labelText, 0);
        Grid.SetColumn(percentText, 1);
        Grid.SetColumn(resetText, 2);
        grid.Children.Add(labelText);
        grid.Children.Add(percentText);
        grid.Children.Add(resetText);
        return grid;
    }

    private static void OpenCreditsDashboard()
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://chatgpt.com/codex/settings/usage")
            {
                UseShellExecute = true,
            });
        }
        catch
        {
        }
    }
}
