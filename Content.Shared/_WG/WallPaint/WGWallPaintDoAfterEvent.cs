using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._WG.WallPaint;

[Serializable, NetSerializable]
public sealed partial class WGWallPaintDoAfterEvent : DoAfterEvent
{
    [DataField]
    public Color Color;

    public WGWallPaintDoAfterEvent(Color color)
    {
        Color = color;
    }

    public override DoAfterEvent Clone() => this;
}
