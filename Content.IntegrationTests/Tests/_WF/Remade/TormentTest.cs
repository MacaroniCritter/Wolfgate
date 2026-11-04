#nullable enable
using Content.Server._WF.Remade.Betrayal;
using Content.Server._WF.Remade.Dread;
using Content.Server._WF.Remade.Torment;
using Robust.Shared.GameObjects;
using static Content.IntegrationTests.Tests._WF.Remade.WFRemadeDirectorTest;

namespace Content.IntegrationTests.Tests._WF.Remade;

/// <summary>The torment smite: one player hallucinates a Crescendo with no stone anywhere, until it is lifted.</summary>
[TestFixture]
public sealed class TormentTest
{
    [Test]
    public async Task TormentBringsTheWholeCrescendoToOnePlayer()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;
        var torment = entMan.System<WFTormentSystem>();
        EntityUid crewman = default;

        await server.WaitPost(() =>
        {
            LayDeck(pair, map);
            crewman = SpawnCrewman(pair, At(map, 0, 0));
            Assert.That(torment.Toggle(crewman), Is.True, "the torment didn't turn on.");
        });

        await pair.RunSeconds(20f);
        await server.WaitAssertion(() =>
        {
            var dread = entMan.GetComponent<WFDreadSubjectComponent>(crewman);
            Assert.That(dread.Level, Is.GreaterThan(0.3f), "the tormented player's sight isn't warped.");
            Assert.That(dread.Bed, Is.GreaterThan(0.5f), "the tormented player has no whisper bed.");
            Assert.That(dread.Whispers + dread.Murmurs, Is.GreaterThan(0), "nothing whispered to the tormented player.");
            Assert.That(dread.Glimpses, Is.GreaterThan(0), "the tormented player saw nothing.");
            Assert.That(entMan.GetComponent<WFBetrayalSubjectComponent>(crewman).ScoreLayers, Is.GreaterThan(0), "no score played.");
            Assert.That(torment.Toggle(crewman), Is.False, "the torment didn't turn off.");
        });

        await pair.RunSeconds(3f);
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<WFDreadSubjectComponent>(crewman).Level, Is.Zero, "the torment outlasted the toggle.");
            Assert.That(entMan.GetComponent<WFDreadSubjectComponent>(crewman).Bed, Is.Zero);
        });
        await pair.CleanReturnAsync();
    }
}
