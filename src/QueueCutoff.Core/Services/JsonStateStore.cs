using System.Text.Json;
using QueueCutoff.Core.Abstractions;
using QueueCutoff.Core.Models;

namespace QueueCutoff.Core.Services;

public sealed class JsonStateStore(string appDataDirectory) : IStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _settingsPath = Path.Combine(appDataDirectory, "settings.json");
    private readonly string _dailyLockPath = Path.Combine(appDataDirectory, "daily-lock.json");

    public static JsonStateStore CreateDefault()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return new JsonStateStore(Path.Combine(root, "QueueCutoff"));
    }

    public async Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        return await LoadAsync(_settingsPath, new AppSettings(), cancellationToken);
    }

    public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        return SaveAsync(_settingsPath, settings, cancellationToken);
    }

    public async Task<DailyLock?> LoadDailyLockAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_dailyLockPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(_dailyLockPath);
        return await JsonSerializer.DeserializeAsync<DailyLock>(stream, JsonOptions, cancellationToken);
    }

    public Task SaveDailyLockAsync(DailyLock dailyLock, CancellationToken cancellationToken = default)
    {
        return SaveAsync(_dailyLockPath, dailyLock, cancellationToken);
    }

    private static async Task<T> LoadAsync<T>(string path, T fallback, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return fallback;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken) ?? fallback;
    }

    private static async Task SaveAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tempPath = $"{path}.tmp";

        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken);
        }

        if (File.Exists(path))
        {
            File.Replace(tempPath, path, null);
        }
        else
        {
            File.Move(tempPath, path);
        }
    }
}
