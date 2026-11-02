using System.Threading;
using System.Threading.Tasks;
using Content.Server._WF.NpcCrew.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;

namespace Content.Server._WF.NpcCrew.HTN;

/// <summary>Plans where a crewman taking someone alive walks next, and does what can be done from there.</summary>
public sealed partial class WFCrewGrabOperator : HTNOperator
{
    private WFCrewGrabberSystem _grabbers = default!;

    [DataField]
    public bool Perform;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _grabbers = sysManager.GetEntitySystem<WFCrewGrabberSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard, CancellationToken cancelToken)
    {
        if (!_grabbers.Destination(blackboard.GetValue<EntityUid>(NPCBlackboard.Owner), out var destination))
            return (false, null);
        return (true, new Dictionary<string, object> { { "WFCrewGrabDestination", destination } });
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        return !Perform || _grabbers.Perform(blackboard.GetValue<EntityUid>(NPCBlackboard.Owner))
            ? HTNOperatorStatus.Finished : HTNOperatorStatus.Continuing;
    }
}
