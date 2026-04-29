using System.Windows;
using QueueCutoff.Core.Models;
using QueueCutoff.Core.Services;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = System.Windows.Controls.TextBox;
using WpfMessageBox = System.Windows.MessageBox;

namespace QueueCutoff.App.Windows;

public sealed class SettingsWindow : Window
{
    private readonly DailyLock? _todayLock;
    private readonly TextBox _cutoffBox;
    private readonly TextBox _resetBox;
    private readonly CheckBox _autostartBox;
    private readonly TextBox _leaguePathBox;
    private readonly TextBox? _todayCutoffBox;

    public SettingsWindow(AppSettings settings, DailyLock? todayLock)
    {
        _todayLock = todayLock;
        Title = "QueueCutoff Settings";
        Width = 460;
        Height = todayLock is null ? 390 : 460;
        MinWidth = 420;
        MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Settings = settings;

        _cutoffBox = new TextBox
        {
            Text = settings.DefaultCutoff.ToString("HH:mm"),
            Margin = new Thickness(0, 4, 0, 12)
        };
        _resetBox = new TextBox
        {
            Text = settings.DayResetTime.ToString("HH:mm"),
            Margin = new Thickness(0, 4, 0, 12)
        };
        _autostartBox = new CheckBox
        {
            Content = "Start QueueCutoff when I sign in",
            IsChecked = settings.AutostartEnabled,
            Margin = new Thickness(0, 0, 0, 12)
        };
        _leaguePathBox = new TextBox
        {
            Text = settings.LeagueInstallPath ?? string.Empty,
            Margin = new Thickness(0, 4, 0, 12)
        };

        var lockText = todayLock is null
            ? "No cutoff has been confirmed for this gaming day."
            : $"This gaming day's cutoff is locked at {todayLock.LockedCutoff:HH:mm}. Default settings affect future gaming days.";

        var save = new Button { Content = "Save", MinWidth = 96, IsDefault = true };
        save.Click += (_, _) => Save();

        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(new TextBlock { Text = lockText, Margin = new Thickness(0, 0, 0, 14) });
        panel.Children.Add(new TextBlock { Text = "Default cutoff (HH:mm; 02:00 means tomorrow morning before reset)" });
        panel.Children.Add(_cutoffBox);
        panel.Children.Add(new TextBlock { Text = "Gaming day reset time (HH:mm)" });
        panel.Children.Add(_resetBox);
        if (todayLock is not null)
        {
            panel.Children.Add(new TextBlock { Text = "Current gaming day cutoff (can only shorten)" });
            _todayCutoffBox = new TextBox
            {
                Text = todayLock.LockedCutoff.ToString("HH:mm"),
                Margin = new Thickness(0, 4, 0, 12)
            };
            panel.Children.Add(_todayCutoffBox);
        }

        panel.Children.Add(_autostartBox);
        panel.Children.Add(new TextBlock { Text = "League install folder (optional)" });
        panel.Children.Add(_leaguePathBox);
        panel.Children.Add(save);
        Content = panel;
    }

    public AppSettings Settings { get; private set; }
    public TimeOnly? TodayCutoff { get; private set; }

    private void Save()
    {
        if (!TimeOnly.TryParse(_cutoffBox.Text, out var cutoff))
        {
            WpfMessageBox.Show("Use a time like 22:10.", "QueueCutoff", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TimeOnly.TryParse(_resetBox.Text, out var resetTime))
        {
            WpfMessageBox.Show("Use a reset time like 08:00.", "QueueCutoff", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_todayCutoffBox is not null && _todayLock is not null)
        {
            if (!TimeOnly.TryParse(_todayCutoffBox.Text, out var todayCutoff))
            {
                WpfMessageBox.Show("Use a time like 22:10.", "QueueCutoff", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (GamingDay.CompareCutoffOrder(_todayLock.Date, todayCutoff, _todayLock.LockedCutoff, _todayLock.DayResetTime) > 0)
            {
                WpfMessageBox.Show("The current gaming day's cutoff can only be shortened.", "QueueCutoff", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            TodayCutoff = todayCutoff;
        }

        Settings = Settings with
        {
            DefaultCutoff = cutoff,
            DayResetTime = resetTime,
            AutostartEnabled = _autostartBox.IsChecked == true,
            LeagueInstallPath = string.IsNullOrWhiteSpace(_leaguePathBox.Text) ? null : _leaguePathBox.Text.Trim()
        };
        DialogResult = true;
    }
}

public sealed record SettingsWindowResult(AppSettings Settings, TimeOnly? TodayCutoff);
