using System.Numerics;
using Content.Server.NPC.HTN;
using Content.Server.Shuttles.Components;
using Content.Shared._WF.Waypoints;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map;

namespace Content.Server._WF.Waypoints;

/// <summary>Adds and deletes the waypoints of the grid a helm or mass scanner stands on.</summary>
public sealed class WFWaypointSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UserInterfaceComponent, WFWaypointAddMessage>(OnAdd);
        SubscribeLocalEvent<UserInterfaceComponent, WFWaypointRemoveMessage>(OnRemove);
    }

    private static bool IsRadarUi(Enum key) => key is RadarConsoleUiKey or ShuttleConsoleUiKey;

    private void OnAdd(Entity<UserInterfaceComponent> ent, ref WFWaypointAddMessage args)
    {
        if (IsRadarUi(args.UiKey))
            TryAdd(ent, args.Name, args.Position);
    }

    private void OnRemove(Entity<UserInterfaceComponent> ent, ref WFWaypointRemoveMessage args)
    {
        if (IsRadarUi(args.UiKey))
            TryRemove(ent, args.Id);
    }

    /// <summary>Whether a helm has an autopilot target, which keeps its planner awake with no living player near.</summary>
    public bool AutopilotEngaged(EntityUid uid, HTNComponent htn)
    {
        return TryComp<ShuttleConsoleComponent>(uid, out var helm) &&
               htn.Blackboard.TryGetValue<EntityCoordinates>(helm.AutopilotTargetKey, out _, EntityManager);
    }

    /// <summary>Marks a waypoint on the console's grid and current map; false when the input or the grid doesn't allow it.</summary>
    public bool TryAdd(EntityUid console, string name, Vector2 position)
    {
        if (!WFWaypointRules.IsValidPosition(position) ||
            Transform(console) is not { GridUid: { } grid, MapUid: { } map })
            return false;

        var waypoints = EnsureComp<WFWaypointsComponent>(grid);
        if (waypoints.Waypoints.Count >= WFWaypointRules.MaxWaypoints)
            return false;

        var id = waypoints.NextId++;
        var clean = WFWaypointRules.CleanName(name);
        if (clean.Length == 0)
            clean = Loc.GetString("wf-waypoint-default-name", ("id", id));

        waypoints.Waypoints.Add(new WFWaypoint(id, clean, GetNetEntity(map), position));
        Dirty(grid, waypoints);
        return true;
    }

    /// <summary>Deletes a waypoint from the console's grid.</summary>
    public bool TryRemove(EntityUid console, int id)
    {
        if (Transform(console).GridUid is not { } grid ||
            !TryComp<WFWaypointsComponent>(grid, out var waypoints) ||
            waypoints.Waypoints.RemoveAll(waypoint => waypoint.Id == id) == 0)
            return false;

        Dirty(grid, waypoints);
        return true;
    }
}
