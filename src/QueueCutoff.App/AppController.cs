using System.Windows;
using System.Windows.Threading;
using QueueCutoff.App.Infrastructure;
using QueueCutoff.App.Windows;
using QueueCutoff.Core.Abstractions;
using QueueCutoff.Core.Models;
using QueueCutoff.Core.Services;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;

namespace QueueCutoff.App;

public sealed class AppController : IAsyncDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly IStateStore _stateStore;
    private readonly DailyLockService _lockService;
    private readonly IClock _clock;
    private readonly EnforcementEngine _engine;
    private readonly IProcessMonitor _processMonitor;
    private readonly ILcuClient _lcuClient;
    private readonly IBlockBackend _blockBackend;
    private readonly IAutostartService _autostart;
    private readonly PlayBreakTracker _playBreakTracker;
    private readonly PeriodicTimer _timer = new(TimeSpan.FromSeconds(5));
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _tickLock = new(1, 1);

    private Task? _loopTask;
    private LeagueProcessSnapshot _snapshot = new(false, false, []);
    private GameflowPhase _phase = GameflowPhase.Unknown;
    private PlayBreakDecision _playBreakDecision = new(false, false, false, false, TimeSpan.Zero, TimeSpan.Zero);
    private bool _isBlocking;
    private string? _activeBlockSignature;
    private bool _settingsOpen;
    private bool _confirmationOpen;
    private bool _lastClientRunning;
    private AppSettings _settings = new();

    public AppController(
        Dispatcher dispatcher,
        IStateStore stateStore,
        DailyLockService lockService,
        IClock clock,
        EnforcementEngine engine,
        IProcessMonitor processMonitor,
        ILcuClient lcuClient,
        IBlockBackend blockBackend,
        IAutostartService autostart,
        PlayBreakTracker playBreakTracker)
    {
        _dispatcher = dispatcher;
        _stateStore = stateStore;
        _lockService = lockService;
        _clock = clock;
        _engine = engine;
        _processMonitor = processMonitor;
        _lcuClient = lcuClient;
        _blockBackend = blockBackend;
        _autostart = autostart;
        _playBreakTracker = playBreakTracker;
        _processMonitor.LeagueProcessesChanged += snapshot => _ = HandleSnapshotAsync(snapshot);
    }

    public event EventHandler? StatusChanged;

    public string StatusText
    {
        get
        {
            var blockText = _isBlocking ? "blocking queue traffic" : "not blocking";
            var leagueText = _snapshot.IsClientRunning ? "League client running" : "League client not running";
            var breakText = _playBreakDecision.IsBreakActive
                ? $" Play break active ({_playBreakDecision.RemainingBreak:mm\\:ss} remaining)."
                : _playBreakDecision.IsBreakPending
                    ? " Play break pending until the current game ends."
                    : string.Empty;
            return $"{leagueText}; phase {_phase}; {blockText}.{breakText}";
        }
    }

    public bool CanExit =>
        !(_settings.PreventExitWhileLeagueRunning && _snapshot.IsClientRunning) &&
        !(_settings.PreventExitWhileEnforcing && _isBlocking);

    public async Task StartAsync()
    {
        _settings = await _stateStore.LoadSettingsAsync(_cts.Token);
        try
        {
            await _autostart.SetEnabledAsync(_settings.AutostartEnabled, _cts.Token);
        }
        catch
        {
            // Autostart sync should not prevent the tray app from running.
        }

        _snapshot = await _processMonitor.GetSnapshotAsync(_cts.Token);
        _lastClientRunning = _snapshot.IsClientRunning;
        _isBlocking = await _blockBackend.GetStatusAsync(_cts.Token);
        await _processMonitor.StartAsync(_cts.Token);
        _loopTask = Task.Run(RunLoopAsync);
        await TickAsync(_cts.Token);
    }

    public async Task OpenSettingsAsync()
    {
        if (_settingsOpen)
        {
            return;
        }

        _settingsOpen = true;
        try
        {
            var settings = await _stateStore.LoadSettingsAsync(_cts.Token);
            var todayLock = await _lockService.GetTodayLockAsync(_cts.Token);

            var settingsResult = await _dispatcher.InvokeAsync<SettingsWindowResult?>(() =>
            {
                var window = new SettingsWindow(settings, todayLock);
                return window.ShowDialog() == true
                    ? new SettingsWindowResult(window.Settings, window.TodayCutoff)
                    : null;
            });

            if (settingsResult is not null)
            {
                await SaveSettingsAsync(settingsResult.Settings);
                if (settingsResult.TodayCutoff.HasValue)
                {
                    await _lockService.ConfirmTodayAsync(settingsResult.TodayCutoff.Value, _cts.Token);
                }
            }
        }
        finally
        {
            _settingsOpen = false;
        }
    }

    public void ShowStatus()
    {
        WpfMessageBox.Show(StatusText, "QueueCutoff", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void RequestExit()
    {
        if (!CanExit)
        {
            WpfMessageBox.Show(
                "QueueCutoff is locked while League is running or enforcement is active.",
                "QueueCutoff",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var app = WpfApplication.Current;
        if (app is not null)
        {
            if (app.Dispatcher.CheckAccess())
            {
                app.Shutdown();
            }
            else
            {
                app.Dispatcher.Invoke(app.Shutdown);
            }

            return;
        }

        if (!_dispatcher.HasShutdownStarted && !_dispatcher.HasShutdownFinished)
        {
            _dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
        }
    }

    private async Task SaveSettingsAsync(AppSettings settings)
    {
        _settings = settings;
        await _stateStore.SaveSettingsAsync(settings, _cts.Token);
        await _autostart.SetEnabledAsync(settings.AutostartEnabled, _cts.Token);
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task RunLoopAsync()
    {
        try
        {
            while (await _timer.WaitForNextTickAsync(_cts.Token))
            {
                await TickAsync(_cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task HandleSnapshotAsync(LeagueProcessSnapshot snapshot)
    {
        UpdateSnapshot(snapshot);
        StatusChanged?.Invoke(this, EventArgs.Empty);

        if (snapshot.IsClientRunning)
        {
            await MaybeShowConfirmationAsync();
        }

        await TickAsync(_cts.Token);
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        if (!await _tickLock.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            _settings = await _stateStore.LoadSettingsAsync(cancellationToken);
            UpdateSnapshot(await _processMonitor.GetSnapshotAsync(cancellationToken));
            var todayLock = await _lockService.GetTodayLockAsync(cancellationToken);

            if (todayLock is null && _isBlocking)
            {
                await _blockBackend.DisableAsync(cancellationToken);
                _isBlocking = false;
                _activeBlockSignature = null;
            }

            if (_snapshot.IsClientRunning)
            {
                await MaybeShowConfirmationAsync();
                _phase = await _lcuClient.GetGameflowPhaseAsync(_settings.LeagueInstallPath, cancellationToken);
            }
            else
            {
                _phase = GameflowPhase.Unknown;
            }

            todayLock = await _lockService.GetTodayLockAsync(cancellationToken);
            var now = _clock.Now;
            var decision = _engine.Decide(now, todayLock, _phase, _snapshot.IsGameRunning);
            _playBreakDecision = _playBreakTracker.Decide(now, _snapshot.IsGameRunning);
            var shouldBlock = decision.ShouldBlock || _playBreakDecision.ShouldBlock;

            if (shouldBlock)
            {
                if (!_snapshot.IsClientRunning)
                {
                    if (_isBlocking)
                    {
                        await _blockBackend.DisableAsync(cancellationToken);
                        _isBlocking = false;
                        _activeBlockSignature = null;
                    }
                }
                else
                {
                    var paths = LeagueProcessPathFilter.GetBlockablePaths(_snapshot.ExecutablePaths);
                    var blockSignature = CreateBlockSignature(paths);
                    if (paths.Count > 0 && decision.ShouldBlock)
                    {
                        await _lockService.MarkEnforcementActivatedAsync(cancellationToken);
                    }

                    if (paths.Count > 0 && (!_isBlocking || _activeBlockSignature != blockSignature))
                    {
                        await _blockBackend.EnableAsync(paths, cancellationToken);
                        if (!_isBlocking)
                        {
                            _dispatcher.Invoke(() => System.Windows.Forms.MessageBox.Show(
                                decision.ShouldBlock
                                    ? "Cutoff reached. Good night."
                                    : "One hour played. Take a 3 minute break.",
                                "QueueCutoff",
                                System.Windows.Forms.MessageBoxButtons.OK,
                                System.Windows.Forms.MessageBoxIcon.Information));
                        }

                        _isBlocking = true;
                        _activeBlockSignature = blockSignature;
                    }

                    if (_playBreakDecision.BreakStarted && _isBlocking && decision.ShouldBlock)
                    {
                        _dispatcher.Invoke(() => System.Windows.Forms.MessageBox.Show(
                            "One hour played. Take a 3 minute break.",
                            "QueueCutoff",
                            System.Windows.Forms.MessageBoxButtons.OK,
                            System.Windows.Forms.MessageBoxIcon.Information));
                    }
                }
            }
            else if (_isBlocking)
            {
                await _blockBackend.DisableAsync(cancellationToken);
                _isBlocking = false;
                _activeBlockSignature = null;
            }

            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            _phase = GameflowPhase.Unknown;
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _tickLock.Release();
        }
    }

    private async Task MaybeShowConfirmationAsync()
    {
        if (_confirmationOpen ||
            _settingsOpen ||
            await _lockService.GetTodayLockAsync(_cts.Token) is not null)
        {
            return;
        }

        _confirmationOpen = true;
        try
        {
            var settings = await _stateStore.LoadSettingsAsync(_cts.Token);
            var cutoff = await _dispatcher.InvokeAsync(() =>
            {
                var window = new ConfirmCutoffWindow(settings.DefaultCutoff, settings.DayResetTime);
                return window.ShowDialog() == true ? window.Cutoff : settings.DefaultCutoff;
            });

            await _lockService.ConfirmTodayAsync(cutoff, _cts.Token);
        }
        finally
        {
            _confirmationOpen = false;
        }
    }

    private void UpdateSnapshot(LeagueProcessSnapshot snapshot)
    {
        if (!_lastClientRunning && snapshot.IsClientRunning)
        {
            _ = Task.Run(() => MaybeShowConfirmationAsync());
        }

        _lastClientRunning = snapshot.IsClientRunning;
        _snapshot = snapshot;
    }

    private static string CreateBlockSignature(IReadOnlyCollection<string> paths)
    {
        return string.Join("|", paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();

        if (_loopTask is not null)
        {
            try
            {
                await _loopTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _timer.Dispose();
        await _processMonitor.DisposeAsync();
        _cts.Dispose();
        _tickLock.Dispose();
    }
}
