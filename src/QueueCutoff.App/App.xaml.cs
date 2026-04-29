using System.IO;
using System.Windows;
using QueueCutoff.App.Infrastructure;
using QueueCutoff.App.Services;
using QueueCutoff.Core.Services;

namespace QueueCutoff.App;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;
    private TrayIconService? _tray;
    private AppController? _controller;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, "QueueCutoff.App.SingleInstance", out var ownsMutex);
        if (!ownsMutex)
        {
            Shutdown();
            return;
        }

        var store = JsonStateStore.CreateDefault();
        var clock = new SystemClock();
        var lockService = new DailyLockService(store, clock);
        var appDirectory = AppContext.BaseDirectory;
        var helperPath = Path.Combine(appDirectory, "QueueCutoff.Elevated.exe");

        var firewall = new ElevatedFirewallBlockBackend(helperPath);
        var processMonitor = new WindowsProcessMonitor();
        var lcuClient = new LcuClient();
        var autostart = new ScheduledTaskAutostart();

        _controller = new AppController(
            Dispatcher,
            store,
            lockService,
            clock,
            new EnforcementEngine(),
            processMonitor,
            lcuClient,
            firewall,
            autostart);

        _tray = new TrayIconService(_controller);
        await _controller.StartAsync();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_controller is not null)
        {
            await _controller.DisposeAsync();
        }

        _tray?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
