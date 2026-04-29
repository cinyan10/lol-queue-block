using QueueCutoff.Core.Models;

namespace QueueCutoff.Core.Abstractions;

public interface ILcuClient
{
    Task<GameflowPhase> GetGameflowPhaseAsync(string? leagueInstallPath, CancellationToken cancellationToken = default);
}
