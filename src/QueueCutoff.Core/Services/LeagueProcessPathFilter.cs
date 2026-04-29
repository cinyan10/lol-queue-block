namespace QueueCutoff.Core.Services;

public static class LeagueProcessPathFilter
{
    private static readonly HashSet<string> BlockableExecutableNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "LeagueClient.exe",
        "LeagueClientUx.exe",
        "LeagueClientUxRender.exe",
        "RiotClientServices.exe"
    };

    public static IReadOnlyCollection<string> GetBlockablePaths(IEnumerable<string> executablePaths)
    {
        return executablePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Where(path => BlockableExecutableNames.Contains(Path.GetFileName(path)))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
