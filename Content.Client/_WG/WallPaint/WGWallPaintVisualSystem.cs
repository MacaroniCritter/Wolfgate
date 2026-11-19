using Content.Shared._WG.WallPaint;
using Robust.Client.GameObjects;
using Robust.Shared.Graphics;
using Robust.Shared.Utility;
using static Robust.Client.GameObjects.SpriteComponent;

namespace Content.Client._WG.WallPaint;

public sealed partial class WGWallPaintVisualSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WGPaintableWallComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<WGPaintableWallComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnStartup(Entity<WGPaintableWallComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp(ent.Owner, out SpriteComponent? sprite))
            return;

        switch (ent.Comp.Type)
        {
            case WGPaintableWallType.Reinforced:
                ReserveCornerLayers(ent.Owner, sprite, "Structures/_WG/WallPaint/reinforced.rsi", "reinf_over0");
                break;
            case WGPaintableWallType.Plastitanium:
                ReserveCornerLayers(ent.Owner, sprite, "Structures/_WG/WallPaint/plastitanium.rsi", "plastitanium0");
                break;
            case WGPaintableWallType.ReinforcedDiagonal:
                ReserveLayer(ent.Owner, sprite, WGWallPaintLayers.Diagonal,
                    "Structures/_WG/WallPaint/reinforced_diagonal.rsi", "state0");
                break;
            case WGPaintableWallType.PlastitaniumDiagonal:
                ReserveLayer(ent.Owner, sprite, WGWallPaintLayers.Diagonal,
                    "Structures/_WG/WallPaint/plastitanium_diagonal.rsi", "state0");
                break;
        }

        ApplyColor(ent, ent.Comp.Color, ent.Comp.Painted);
    }

    private void OnAppearanceChange(Entity<WGPaintableWallComponent> ent, ref AppearanceChangeEvent args)
    {
        var color = ent.Comp.Color;
        var painted = ent.Comp.Painted;

        if (args.AppearanceData.TryGetValue(WGWallPaintVisuals.Color, out var colorValue) &&
            colorValue is Color appearanceColor)
        {
            color = appearanceColor;
        }

        if (args.AppearanceData.TryGetValue(WGWallPaintVisuals.Painted, out var paintedValue) &&
            paintedValue is bool appearancePainted)
        {
            painted = appearancePainted;
        }

        ApplyColor(ent, color, painted);
    }

    private void ReserveCornerLayers(EntityUid uid, SpriteComponent sprite, string rsi, string state)
    {
        ReserveLayer(uid, sprite, WGWallPaintLayers.SouthEast, rsi, state,
            DirectionOffset.None);
        ReserveLayer(uid, sprite, WGWallPaintLayers.NorthEast, rsi, state,
            DirectionOffset.CounterClockwise);
        ReserveLayer(uid, sprite, WGWallPaintLayers.NorthWest, rsi, state,
            DirectionOffset.Flip);
        ReserveLayer(uid, sprite, WGWallPaintLayers.SouthWest, rsi, state,
            DirectionOffset.Clockwise);
    }

    private void ReserveLayer(EntityUid uid, SpriteComponent sprite, WGWallPaintLayers key, string rsi, string state,
        DirectionOffset offset = DirectionOffset.None)
    {
        var layer = _sprite.LayerMapReserve((uid, sprite), key);
        _sprite.LayerSetSprite((uid, sprite), layer, new SpriteSpecifier.Rsi(new ResPath(rsi), state));
        _sprite.LayerSetDirOffset((uid, sprite), layer, offset);
        _sprite.LayerSetVisible((uid, sprite), layer, false);
    }

    private void ApplyColor(Entity<WGPaintableWallComponent> ent, Color color, bool painted)
    {
        if (!TryComp(ent.Owner, out SpriteComponent? sprite))
            return;

        switch (ent.Comp.Type)
        {
            case WGPaintableWallType.Reinforced:
            case WGPaintableWallType.Plastitanium:
                SetLayer(ent.Owner, sprite, WGWallPaintLayers.NorthEast, color, painted);
                SetLayer(ent.Owner, sprite, WGWallPaintLayers.SouthEast, color, painted);
                SetLayer(ent.Owner, sprite, WGWallPaintLayers.SouthWest, color, painted);
                SetLayer(ent.Owner, sprite, WGWallPaintLayers.NorthWest, color, painted);
                break;
            case WGPaintableWallType.ReinforcedDiagonal:
            case WGPaintableWallType.PlastitaniumDiagonal:
                SetLayer(ent.Owner, sprite, WGWallPaintLayers.Diagonal, color, painted);
                break;
        }
    }

    private void SetLayer(EntityUid uid, SpriteComponent sprite, WGWallPaintLayers key, Color color, bool visible)
    {
        if (!_sprite.LayerMapTryGet((uid, sprite), key, out var layer, false))
            return;

        _sprite.LayerSetColor((uid, sprite), layer, color);
        _sprite.LayerSetVisible((uid, sprite), layer, visible);
    }
}
