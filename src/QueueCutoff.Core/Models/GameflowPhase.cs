namespace QueueCutoff.Core.Models;

public enum GameflowPhase
{
    Unknown,
    None,
    Lobby,
    Matchmaking,
    ReadyCheck,
    ChampSelect,
    GameStart,
    InProgress,
    Reconnect,
    EndOfGame
}
