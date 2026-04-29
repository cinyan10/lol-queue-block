using System.Windows;
using QueueCutoff.Core.Models;
using Button = System.Windows.Controls.Button;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = System.Windows.Controls.TextBox;
using WpfMessageBox = System.Windows.MessageBox;

namespace QueueCutoff.App.Windows;

public sealed class ConfirmCutoffWindow : Window
{
    private readonly TextBox _cutoffBox;

    public ConfirmCutoffWindow(TimeOnly defaultCutoff, TimeOnly dayResetTime)
    {
        Title = "Confirm cutoff";
        Width = 380;
        Height = 220;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;

        _cutoffBox = new TextBox
        {
            Text = defaultCutoff.ToString("HH:mm"),
            Margin = new Thickness(0, 4, 0, 12)
        };

        var confirm = new Button { Content = "Confirm", MinWidth = 96, IsDefault = true };
        confirm.Click += (_, _) => Confirm();

        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(new TextBlock
        {
            Text = "Lock this gaming day's League queue cutoff.",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"Gaming day resets at {dayResetTime:HH:mm}. Early morning times count before that reset.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        });
        panel.Children.Add(new TextBlock { Text = "Cutoff time (HH:mm)" });
        panel.Children.Add(_cutoffBox);
        panel.Children.Add(confirm);
        Content = panel;
    }

    public TimeOnly Cutoff { get; private set; }

    private void Confirm()
    {
        if (!TimeOnly.TryParse(_cutoffBox.Text, out var cutoff))
        {
            WpfMessageBox.Show("Use a time like 22:10.", "QueueCutoff", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Cutoff = cutoff;
        DialogResult = true;
    }
}
