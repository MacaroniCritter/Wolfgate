using Content.Shared._WG.WallPaint;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using static Robust.Client.GameObjects.SpriteComponent;

namespace Content.Client.IconSmoothing;

public sealed partial class IconSmoothSystem
{
    private void SetWallPaintCornerState(SpriteComponent sprite, CornerLayers corner, DirectionOffset offset, string state)
    {
        if (!TryComp(sprite.Owner, out WGPaintableWallComponent? paint) ||
            paint.Type is WGPaintableWallType.ReinforcedDiagonal or WGPaintableWallType.PlastitaniumDiagonal)
        {
            return;
        }

        var layer = corner switch
        {
            CornerLayers.NE => WGWallPaintLayers.NorthEast,
            CornerLayers.SE => WGWallPaintLayers.SouthEast,
            CornerLayers.SW => WGWallPaintLayers.SouthWest,
            CornerLayers.NW => WGWallPaintLayers.NorthWest,
            _ => throw new ArgumentOutOfRangeException(nameof(corner), corner, null),
        };

        if (_sprite.LayerMapTryGet((sprite.Owner, sprite), layer, out var index, false))
        {
            _sprite.LayerSetRsiState((sprite.Owner, sprite), index, new RSI.StateId(state));
            _sprite.LayerSetDirOffset((sprite.Owner, sprite), index, offset);
        }
    }

    private void SetWallPaintDiagonalState(Entity<SpriteComponent> sprite, string state)
    {
        if (!TryComp(sprite.Owner, out WGPaintableWallComponent? paint) ||
            paint.Type is not (WGPaintableWallType.ReinforcedDiagonal or WGPaintableWallType.PlastitaniumDiagonal) ||
            !_sprite.LayerMapTryGet((sprite.Owner, sprite.Comp), WGWallPaintLayers.Diagonal, out var index, false))
        {
            return;
        }

        _sprite.LayerSetRsiState((sprite.Owner, sprite.Comp), index, new RSI.StateId(state));
    }
}
