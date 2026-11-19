using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._WG.WallPaint;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WGPaintableWallComponent : Component
{
    [DataField("paintType", required: true)]
    public WGPaintableWallType Type;

    [DataField, AutoNetworkedField]
    public Color Color = Color.White;

    [DataField, AutoNetworkedField]
    public bool Painted;
}

[Serializable, NetSerializable]
public enum WGPaintableWallType : byte
{
    Reinforced,
    Plastitanium,
    ReinforcedDiagonal,
    PlastitaniumDiagonal,
}

[Serializable, NetSerializable]
public enum WGWallPaintVisuals : byte
{
    Color,
    Painted,
}

public enum WGWallPaintLayers : byte
{
    NorthEast,
    SouthEast,
    SouthWest,
    NorthWest,
    Diagonal,
}
