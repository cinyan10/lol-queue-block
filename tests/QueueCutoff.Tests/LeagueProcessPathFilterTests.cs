using QueueCutoff.Core.Services;
using Xunit;

namespace QueueCutoff.Tests;

public sealed class LeagueProcessPathFilterTests
{
    [Fact]
    public void GetBlockablePaths_ExcludesGameBinaryAndKeepsClientAndRiotLauncherPaths()
    {
        var paths = LeagueProcessPathFilter.GetBlockablePaths(
        [
            @"C:\Riot Games\League of Legends\LeagueClient.exe",
            @"C:\Riot Games\League of Legends\Game\League of Legends.exe",
            @"C:\Riot Games\League of Legends\LeagueClientUx.exe",
            @"C:\Riot Games\Riot Client\RiotClientServices.exe",
            @"C:\Riot Games\Riot Client\UX\RiotClientUx.exe"
        ]);

        Assert.Contains(paths, path => path.EndsWith("LeagueClient.exe", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(paths, path => path.EndsWith("LeagueClientUx.exe", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(paths, path => path.EndsWith("RiotClientServices.exe", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(paths, path => path.EndsWith("RiotClientUx.exe", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(paths, path => path.EndsWith("League of Legends.exe", StringComparison.OrdinalIgnoreCase));
    }
}
