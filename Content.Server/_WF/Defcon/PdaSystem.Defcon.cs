using Content.Server._WF.Defcon;

namespace Content.Server.PDA;

/// <summary>Shows the sector DEFCON on the PDA, under the war level.</summary>
public sealed partial class PdaSystem
{
    [Dependency] private WFDefconSystem _defcon = default!;

    private void InitializeDefcon()
    {
        SubscribeLocalEvent<WFDefconChangedEvent>(_ => UpdateAllPdaUisOnStation());
    }

    private string? WithDefcon(string? warLevel)
    {
        return _defcon.PdaLine() is { } line ? $"{warLevel}\n{line}" : warLevel;
    }
}
