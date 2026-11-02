using Content.Shared._WF.Encounters;

namespace Content.Server._WF.Encounters.Components;

/// <summary>What an abductor is doing.</summary>
public enum WFAbductPhase : byte
{
    /// <summary>Flying its wander legs until the next run is due.</summary>
    Roam,

    /// <summary>Closing on its mark.</summary>
    Close,

    /// <summary>Getting a port on its mark.</summary>
    Dock,

    /// <summary>Docked, or with a prisoner loose aboard: its hands are stunning, cuffing and dragging.</summary>
    Grab,

    /// <summary>Clear of the mark with someone in its cell.</summary>
    Ransom,
}

/// <summary>An abductor's round: its mark, its prisoner, its ransom and the ships it leaves alone.</summary>
public sealed class WFAbductState
{
    public WFEncounterAbduct Config = new();

    public WFAbductPhase Phase;

    /// <summary>When it entered <see cref="Phase"/>.</summary>
    public TimeSpan PhaseSince;

    /// <summary>The ship it is after, and the home a prisoner goes back to.</summary>
    public EntityUid? Mark;

    /// <summary>Who it is taking, or holds.</summary>
    public EntityUid? Prisoner;

    /// <summary>The chair its prisoner goes on.</summary>
    public EntityUid? Cell;

    /// <summary>Failed runs at getting a port on the mark.</summary>
    public int DockMisses;

    /// <summary>Its people went aboard the mark, and any still there when it casts off are left.</summary>
    public bool Strays;

    /// <summary>When it next picks a mark.</summary>
    public TimeSpan NextMark;

    /// <summary>When it gives up on the ransom. Null until someone is in the cell.</summary>
    public TimeSpan? RansomEnds;

    /// <summary>When it may next hail the prisoner's ship, and how many hails that ship has had.</summary>
    public TimeSpan NextHail;

    public int Hails;

    /// <summary>The prisoner's ship said no: it stops calling.</summary>
    public bool Refused;

    /// <summary>The console and crewman its hails go through.</summary>
    public EntityUid? Console;

    public EntityUid? Operator;

    /// <summary>When it closes a channel it has said its last line on.</summary>
    public TimeSpan? CloseAt;

    /// <summary>Ships it leaves alone, and until when.</summary>
    public Dictionary<EntityUid, TimeSpan> Spared = new();

    /// <summary>Prisoners taken, ransoms paid and their sum.</summary>
    public int Taken;

    public int Ransoms;

    public int Collected;
}
