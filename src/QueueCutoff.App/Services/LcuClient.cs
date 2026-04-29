using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using QueueCutoff.Core.Abstractions;
using QueueCutoff.Core.Models;

namespace QueueCutoff.App.Services;

public sealed class LcuClient : ILcuClient
{
    public async Task<GameflowPhase> GetGameflowPhaseAsync(string? leagueInstallPath, CancellationToken cancellationToken = default)
    {
        var lockfile = FindLockfile(leagueInstallPath);
        if (lockfile is null)
        {
            return GameflowPhase.Unknown;
        }

        var info = await LockfileInfo.ReadAsync(lockfile, cancellationToken);
        if (info is null)
        {
            return GameflowPhase.Unknown;
        }

        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri($"{info.Protocol}://127.0.0.1:{info.Port}")
        };

        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"riot:{info.Password}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);

        try
        {
            var json = await client.GetStringAsync("/lol-gameflow/v1/gameflow-phase", cancellationToken);
            var phaseText = JsonSerializer.Deserialize<string>(json);
            return Enum.TryParse<GameflowPhase>(phaseText, ignoreCase: true, out var phase)
                ? phase
                : GameflowPhase.Unknown;
        }
        catch
        {
            return GameflowPhase.Unknown;
        }
    }

    private static string? FindLockfile(string? leagueInstallPath)
    {
        var runningClientLockfile = FindRunningClientLockfile();
        if (runningClientLockfile is not null)
        {
            return runningClientLockfile;
        }

        if (!string.IsNullOrWhiteSpace(leagueInstallPath))
        {
            var candidate = Path.Combine(leagueInstallPath, "lockfile");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var defaultPath = Path.Combine(
            Environment.GetEnvironmentVariable("SystemDrive") ?? "C:",
            "Riot Games",
            "League of Legends",
            "lockfile");

        return File.Exists(defaultPath) ? defaultPath : null;
    }

    private static string? FindRunningClientLockfile()
    {
        foreach (var process in Process.GetProcessesByName("LeagueClient"))
        {
            using (process)
            {
                try
                {
                    var exePath = process.MainModule?.FileName;
                    var directory = Path.GetDirectoryName(exePath);
                    if (directory is null)
                    {
                        continue;
                    }

                    var lockfile = Path.Combine(directory, "lockfile");
                    if (File.Exists(lockfile))
                    {
                        return lockfile;
                    }
                }
                catch
                {
                }
            }
        }

        return null;
    }

    private sealed record LockfileInfo(int Port, string Password, string Protocol)
    {
        public static async Task<LockfileInfo?> ReadAsync(string path, CancellationToken cancellationToken)
        {
            var text = await File.ReadAllTextAsync(path, cancellationToken);
            var parts = text.Split(':');
            if (parts.Length < 5 || !int.TryParse(parts[2], out var port))
            {
                return null;
            }

            return new LockfileInfo(port, parts[3], parts[4]);
        }
    }
}
