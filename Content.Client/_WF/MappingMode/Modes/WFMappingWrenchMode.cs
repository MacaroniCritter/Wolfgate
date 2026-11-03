using Content.Shared._WF.MappingTools;
using Content.Shared.Construction.Components;
using Robust.Shared.Input.Binding;

namespace Content.Client._WF.MappingMode.Modes;

/// <summary>Wrench: a left click anchors or unanchors what is under it, as a wrench would.</summary>
public sealed partial class WFMappingWrenchMode(WFMappingBuildModeSystem modes) : WFMappingModeHandlerBase(modes)
{
    [Dependency] private IEntityManager _entities = default!;

    public override WFMappingBuildMode Mode => WFMappingBuildMode.Wrench;

    public override bool Primary(in PointerInputCmdHandler.PointerInputCmdArgs args)
    {
        if (!IsDown(args))
            return true;

        var clicked = _entities.HasComponent<AnchorableComponent>(args.EntityUid) ? args.EntityUid : (EntityUid?) null;
        if ((clicked ?? Modes.AnchorableAt(Modes.ToMap(args.Coordinates))) is { } target)
            Modes.Send(new MappingToolsWrenchEvent(_entities.GetNetEntity(target)));
        else
            Modes.Popup("wf-mapping-wrench-none");

        return true;
    }

    public override bool Secondary(in PointerInputCmdHandler.PointerInputCmdArgs args)
    {
        return true;
    }
}
