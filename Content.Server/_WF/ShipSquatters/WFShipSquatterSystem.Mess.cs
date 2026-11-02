using Content.Server.Fluids.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server._WF.ShipSquatters;

public sealed partial class WFShipSquatterSystem
{
    [Dependency] private PuddleSystem _puddles = default!;

    /// <summary>Tiles round him that he makes mess on.</summary>
    private const int MessReach = 3;

    /// <summary>
    /// Now and then drops something near him: litter, a puddle or graffiti, up to a cap. All of it comes off with a mop
    /// or by hand. He puts it on bare floor in his own rooms, never on a tile with anything anchored (walls, pipes,
    /// cables, machines), beside a door or on another puddle, and never touches what is already there.
    /// </summary>
    private void MakeMess(Entity<WFShipSquatterComponent> ent, TransformComponent xform, MapGridComponent grid, TimeSpan now)
    {
        var comp = ent.Comp;
        if (comp.MessCount >= comp.MessCap || now < comp.NextMess)
            return;

        comp.NextMess = now + Seconds(comp.MessMin, comp.MessMax);
        ScanRooms(ent, grid);
        var here = _maps.CoordinatesToTile(comp.Ship, grid, xform.Coordinates);
        var options = new List<Vector2i>();
        for (var x = -MessReach; x <= MessReach; x++)
        {
            for (var y = -MessReach; y <= MessReach; y++)
            {
                var tile = here + new Vector2i(x, y);
                if (comp.Rooms.Contains(tile) && IsBare(comp, grid, tile))
                    options.Add(tile);
            }
        }

        if (options.Count == 0)
            return;

        var at = _maps.GridTileToLocal(comp.Ship, grid, _random.Pick(options));
        var roll = _random.NextFloat();
        if (roll < 0.5f && comp.Litter.Count > 0)
            comp.Mess.Add(Spawn(_random.Pick(comp.Litter), at.Offset(_random.NextVector2(0.3f))));
        else if (roll < 0.8f && comp.Puddles.Count > 0)
        {
            var spill = new Solution(_random.Pick(comp.Puddles), FixedPoint2.New(comp.PuddleVolume));
            if (!_puddles.TrySpillAt(at, spill, out var puddle, false))
                return;
            comp.Mess.Add(puddle);
        }
        else
            Spawn(comp.Graffiti, at);

        comp.MessCount++;
    }

    /// <summary>Whether a tile has nothing anchored on it and no door beside it.</summary>
    private bool IsBare(WFShipSquatterComponent comp, MapGridComponent grid, Vector2i tile)
    {
        if (_maps.GetAnchoredEntitiesEnumerator(comp.Ship, grid, tile).MoveNext(out _))
            return false;

        foreach (var step in Steps)
        {
            if (comp.Doors.Contains(tile + step))
                return false;
        }

        return !_puddles.TryGetPuddle(_maps.GetTileRef(comp.Ship, grid, tile), out _);
    }
}
