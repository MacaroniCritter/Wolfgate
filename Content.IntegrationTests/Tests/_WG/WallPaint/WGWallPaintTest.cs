using Content.Server._WG.WallPaint;
using Content.Shared._WG.WallPaint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._WG.WallPaint;

[TestFixture]
public sealed class WGWallPaintTest
{
    [Test]
    public async Task PaintingPersistsColorOnEachWallKind()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var wallSystem = entMan.System<WGWallPaintSystem>();
        var color = Color.FromHex("#35A2D6");
        var walls = new (string Prototype, WGPaintableWallType Type)[]
        {
            ("WallReinforced", WGPaintableWallType.Reinforced),
            ("WallReinforcedDiagonal", WGPaintableWallType.ReinforcedDiagonal),
            ("WallPlastitanium", WGPaintableWallType.Plastitanium),
            ("WallPlastitaniumDiagonal", WGPaintableWallType.PlastitaniumDiagonal),
        };

        await server.WaitAssertion(() =>
        {
            for (var i = 0; i < walls.Length; i++)
            {
                var (prototype, type) = walls[i];
                var wall = entMan.SpawnEntity(prototype, new EntityCoordinates(map.Grid, i * 2 + 0.5f, 0.5f));
                var paintable = entMan.GetComponent<WGPaintableWallComponent>(wall);
                Assert.That(paintable.Type, Is.EqualTo(type));

                wallSystem.PaintWall((wall, paintable), color);

                Assert.That(paintable.Painted, Is.True);
                Assert.That(paintable.Color, Is.EqualTo(color));
            }
        });

        await pair.CleanReturnAsync();
    }
}
