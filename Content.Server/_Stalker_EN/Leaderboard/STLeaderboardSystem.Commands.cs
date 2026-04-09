using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Stalker_EN.Leaderboard;

public sealed partial class STLeaderboardSystem
{
    [AdminCommand(AdminFlags.Server)]
    private void LeaderboardClearCommand(IConsoleShell shell, string argStr, string[] args)
    {
        var count = _knownStalkers.Count;
        ClearLeaderboard();
        shell.WriteLine($"Leaderboard cleared. Removed {count} entries.");
    }

    [AdminCommand(AdminFlags.Server)]
    private void LeaderboardListCommand(IConsoleShell shell, string argStr, string[] args)
    {
        shell.WriteLine($"Leaderboard entries ({_knownStalkers.Count}):");
        foreach (var (key, val) in _knownStalkers)
        {
            shell.WriteLine($"  [{key.UserId}] {val.Name} | Band: {val.Band} | Rank: {val.RankIndex}");
        }
    }

    [AdminCommand(AdminFlags.Server)]
    private void LeaderboardRemoveCommand(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1)
        {
            shell.WriteError("Usage: leaderboard-remove <name>");
            return;
        }

        // Support multi-word names: leaderboard-remove "John Doe"
        var name = string.Join(" ", args).Trim('"');
        var removed = 0;
        var keysToRemove = _knownStalkers
            .Where(kv => kv.Value.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _knownStalkers.Remove(key);
            removed++;
        }

        if (removed > 0)
        {
            BroadcastUiState();
            shell.WriteLine($"Removed {removed} entry(ies) matching '{name}'.");
        }
        else
        {
            shell.WriteError($"No entries found matching '{name}'.");
        }
    }
}
