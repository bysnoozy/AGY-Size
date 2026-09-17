using Avalonia.Controls;
using Avalonia.Layout;

namespace AgySize.App.Views;

/// <summary>Petite boîte de dialogue oui/non construite en code, sans fichier AXAML dédié.</summary>
public static class ConfirmDialog
{
    public static async Task<bool> ShowAsync(Window owner, string title, string message)
    {
        var tcs = new TaskCompletionSource<bool>();

        var window = new Window
        {
            Title = title,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        var yesButton = new Button { Content = "Oui", Width = 90, Margin = new Avalonia.Thickness(0, 0, 8, 0) };
        var noButton = new Button { Content = "Non", Width = 90, IsDefault = true };

        yesButton.Click += (_, _) =>
        {
            tcs.TrySetResult(true);
            window.Close();
        };

        noButton.Click += (_, _) =>
        {
            tcs.TrySetResult(false);
            window.Close();
        };

        window.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(16),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Children = { yesButton, noButton },
                },
            },
        };

        window.Closed += (_, _) => tcs.TrySetResult(false);

        await window.ShowDialog(owner);
        return await tcs.Task;
    }
}
