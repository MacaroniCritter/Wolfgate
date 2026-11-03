using Content.Server.Atmos.Components;
using Content.Server.Chat.Managers;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    [Dependency] private IChatManager _wfChat = default!;

    /// <summary>A gap this long between two depressurizations from one tile ends its run.</summary>
    private static readonly TimeSpan WFBreachGap = TimeSpan.FromSeconds(5);

    /// <summary>How long one tile may keep depressurizing before it counts as a refill loop; a real breach drains well within it.</summary>
    private static readonly TimeSpan WFBreachLoopTime = TimeSpan.FromMinutes(3);

    /// <summary>How long a looping tile leaks through ordinary gas sharing instead of space wind.</summary>
    private static readonly TimeSpan WFBreachQuietTime = TimeSpan.FromMinutes(10);

    /// <summary>Runs of explosive depressurization per grid and starting tile.</summary>
    private readonly Dictionary<(EntityUid Grid, Vector2i Tile), WFBreachRun> _wfBreachRuns = new();

    private TimeSpan _wfNextBreachPrune;

    private sealed class WFBreachRun
    {
        public TimeSpan Start;
        public TimeSpan Last;
        public TimeSpan QuietUntil;
        public bool Alerted;
    }

    /// <summary>
    /// Whether a depressurization from this tile should be skipped because the room behind it keeps refilling into a
    /// hole: the same tile venting nonstop throws items, plays wind and resends physics to everyone near it forever.
    /// The room still leaks to space through normal gas sharing while skipped. Public for tests.
    /// </summary>
    public bool WFBreachLooping(EntityUid grid, TileAtmosphere tile)
    {
        var now = _gameTiming.CurTime;
        WFPruneBreachRuns(now);

        var key = (grid, tile.GridIndices);
        if (!_wfBreachRuns.TryGetValue(key, out var run))
        {
            _wfBreachRuns[key] = new WFBreachRun { Start = now, Last = now };
            return false;
        }

        // A loop still going when its quiet ends goes straight back to quiet. It alerts once per run, not every ten minutes.
        if (now < run.QuietUntil)
        {
            run.Last = now;
            return true;
        }

        if (now - run.Last > WFBreachGap)
            run.Start = now;
        run.Last = now;

        if (now - run.Start < WFBreachLoopTime)
            return false;

        run.Start = now;
        run.QuietUntil = now + WFBreachQuietTime;
        if (run.Alerted)
            return true;

        run.Alerted = true;
        var name = ToPrettyString(grid);
        Log.Info($"Explosive depressurization from {tile.GridIndices} on {name} has looped for {WFBreachLoopTime.TotalMinutes} minutes; quieting it.");
        _wfChat.SendAdminAlert(Loc.GetString("wf-atmos-breach-loop-alert",
            ("grid", name.ToString()), ("x", tile.GridIndices.X), ("y", tile.GridIndices.Y),
            ("minutes", WFBreachLoopTime.TotalMinutes)));
        return true;
    }

    /// <summary>
    /// Marks the tiles a quieted zone search reached as done this cycle, so the room's other active tiles don't repeat
    /// the search. Gas sharing between them is untouched.
    /// </summary>
    private void WFMarkEqualized(int tileCount, int cycleNum)
    {
        for (var i = 0; i < tileCount; i++)
        {
            if (_equalizeTiles[i] is { } tile)
                tile.MonstermosInfo.LastCycle = cycleNum;
        }
    }

    /// <summary>Drops runs that ended and are no longer quieted, once a minute.</summary>
    private void WFPruneBreachRuns(TimeSpan now)
    {
        if (now < _wfNextBreachPrune)
            return;

        _wfNextBreachPrune = now + TimeSpan.FromMinutes(1);
        foreach (var (key, run) in _wfBreachRuns)
        {
            if (now - run.Last > WFBreachGap && now >= run.QuietUntil)
                _wfBreachRuns.Remove(key);
        }
    }
}
