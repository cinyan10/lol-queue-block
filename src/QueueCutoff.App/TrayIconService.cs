using System.Drawing;
using System.Windows.Forms;
using System.Windows.Threading;

namespace QueueCutoff.App;

public sealed class TrayIconService : IDisposable
{
    private readonly AppController _controller;
    private readonly Dispatcher _dispatcher;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _exitItem;

    public TrayIconService(AppController controller)
    {
        _controller = controller;
        _dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        _statusItem = new ToolStripMenuItem("Starting...");
        _exitItem = new ToolStripMenuItem("Exit");

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings", null, async (_, _) => await _controller.OpenSettingsAsync());
        menu.Items.Add("Status", null, (_, _) => _controller.ShowStatus());
        menu.Items.Add(_exitItem);

        _statusItem.Enabled = false;
        _exitItem.ForeColor = SystemColors.GrayText;
        _exitItem.Click += (_, _) => _controller.RequestExit();

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Shield,
            Text = "QueueCutoff",
            ContextMenuStrip = menu,
            Visible = true
        };

        _notifyIcon.DoubleClick += async (_, _) => await _controller.OpenSettingsAsync();
        _controller.StatusChanged += (_, _) => _dispatcher.BeginInvoke(new Action(Refresh));
        Refresh();
    }

    private void Refresh()
    {
        _statusItem.Text = _controller.StatusText;
        _exitItem.Enabled = true;
        _notifyIcon.Text = _controller.StatusText.Length > 63
            ? _controller.StatusText[..63]
            : _controller.StatusText;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
