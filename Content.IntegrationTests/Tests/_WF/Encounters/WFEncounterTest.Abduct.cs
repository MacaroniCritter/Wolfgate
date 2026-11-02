#nullable enable
using System.Collections.Generic;
using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Content.Server._NF.Bank;
using Content.Server._WF.Encounters.Components;
using Content.Server._WF.Encounters.Systems;
using Content.Server._WF.NpcCrew.Components;
using Content.Server._WF.NpcCrew.Systems;
using Content.Shared._NF.Bank.Components;
using Content.Shared._NF.Shipyard.Prototypes;
using Content.Shared._WF.Encounters;
using Content.Shared.Buckle.Components;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Doors.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Verbs;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._WF.Encounters;

/// <summary>
/// Abductors: their crew take someone alive and hold them in a cell. A paid ransom or a run-out timer sends the prisoner
/// home with their cuffs off; a dead crew leaves the door open.
/// </summary>
public sealed partial class WFEncounterTest
{
    private const int AbductRansomAmount = 5000;

    [TestPrototypes]
    private const string AbductPrototypes = @"
- type: wfEncounter
  id: WFTestAbductor
  name: wf-encounter-name-convoy
  start: Manual
  lifetime: Persistent
  bounty:
    ship: abductor
    target: Crew
    spesos: 10000
    title: wf-bounty-title-abductor-light
    description: wf-bounty-desc-abductor-light
  ships:
  - key: abductor
    vessel: Hazel
    side: pirates
    profile: WFCrewProfileAbductor
    captain: false
    deckhands: 2
    guards: 0
    evades: false
    abduct:
      ransom: 5000
      markMin: 100000
      markMax: 100000
      holdTime: 100000
";

    /// <summary>Every hull an abductor can fly has a cell chair with a door to bolt.</summary>
    [Test]
    public async Task AbductorHullsHaveACell()
    {
        var protos = Server.ResolveDependency<IPrototypeManager>();
        var resources = Server.ResolveDependency<IResourceManager>();
        await Server.WaitAssertion(() =>
        {
            var checkedHulls = 0;
            foreach (var encounter in protos.EnumeratePrototypes<WFEncounterPrototype>())
            {
                foreach (var ship in encounter.Ships.Where(ship => ship.Abduct != null))
                {
                    var vessels = ship.Tiers.SelectMany(tier => tier.Vessels).Concat(ship.Vessels).ToList();
                    if (ship.Vessel is { } single)
                        vessels.Add(single);
                    Assert.That(vessels, Is.Not.Empty, $"{encounter.ID} flies nothing.");
                    foreach (var id in vessels)
                    {
                        var path = protos.Index<VesselPrototype>(id).ShuttlePath;
                        Assert.That(resources.ContentFileReadAllText(path).Contains("- proto: WFAbductCellChair"), Is.True,
                            $"{encounter.ID} flies {id}, which has no WFAbductCellChair.");
                        checkedHulls++;
                    }
                }
            }

            Assert.That(checkedHulls, Is.GreaterThan(0));
        });
    }

    /// <summary>A scene: a real abductor with a free tile for the test player on its own deck.</summary>
    private async Task<(EntityUid Encounter, WFEncounterShipState Ship, WFAbductState Abduct, EntityUid Cell)> StartAbductor(Vector2 at)
    {
        EntityUid encounter = default;
        WFEncounterShipState ship = default!;
        EntityUid cell = default;
        await Server.WaitAssertion(() =>
        {
            var proto = Server.ResolveDependency<IPrototypeManager>().Index<WFEncounterPrototype>("WFTestAbductor");
            Assert.That(Server.System<WFEncounterSystem>().TrySpawn(proto, new MapCoordinates(at, MapData.MapId), out encounter), Is.True);
            ship = SEntMan.GetComponent<WFEncounterComponent>(encounter).Ships["abductor"];
        });

        await WaitUntilServer(() => !ship.Building && !ship.Virtual, 600);
        await Server.WaitAssertion(() =>
        {
            var found = Server.System<WFAbductCellSystem>().FindCell(ship.Grid);
            Assert.That(found, Is.Not.Null, "The hull has a cell.");
            cell = found!.Value;
            Assert.That(ship.Abduct, Is.Not.Null);
        });

        return (encounter, ship, ship.Abduct!, cell);
    }

    /// <summary>Crew stun, cuff and drag a mob to the chair across a real hull, door included, and it stays alive.</summary>
    [Test]
    public async Task GrabbersStunCuffAndStrapSomeoneIn()
    {
        var (_, ship, _, cell) = await StartAbductor(new Vector2(3000f, 3000f));
        EntityUid captive = default;
        await Server.WaitAssertion(() =>
        {
            // Outside the cell, by the door; the player stands aboard too, or the crew's AI stays asleep.
            captive = SEntMan.SpawnEntity("MobHuman", Transform(ship.Grid, new Vector2(4.5f, -3.5f)));
            Server.System<SharedTransformSystem>().SetCoordinates(SPlayer, Transform(ship.Grid, new Vector2(4.5f, -3.5f)));
            var grabbers = Server.System<WFCrewGrabberSystem>();
            var crew = SEntMan.EntityQueryEnumerator<WFCrewComponent>();
            var sent = 0;
            while (crew.MoveNext(out var uid, out var member))
            {
                if (member.Group != ship.Group || member.Role != "WFCrewDeckhand")
                    continue;
                grabbers.Order(uid, captive, cell);
                sent++;
            }

            Assert.That(sent, Is.GreaterThan(0), "There are hands to send.");
        });

        var taken = false;
        for (var i = 0; i < 60 && !taken; i++)
        {
            await RunTicks(100);
            await Server.WaitPost(() => taken = SEntMan.TryGetComponent<BuckleComponent>(captive, out var buckle) && buckle.BuckledTo == cell);
        }

        await Server.WaitAssertion(() =>
        {
            var state = new System.Text.StringBuilder();
            var xform = SEntMan.GetComponent<TransformComponent>(captive);
            state.Append($"captive at {xform.Coordinates}, cuffed {SEntMan.GetComponent<CuffableComponent>(captive).CuffedHandCount}, ");
            var stamina = SEntMan.GetComponent<StaminaComponent>(captive);
            state.Append($"stamina crit {stamina.Critical} dmg {stamina.StaminaDamage} of {stamina.CritThreshold}, hands {SEntMan.GetComponentOrNull<Content.Shared.Hands.Components.HandsComponent>(captive)?.Count}, ");
            state.Append($"puller {SEntMan.GetComponent<Content.Shared.Movement.Pulling.Components.PullableComponent>(captive).Puller}, ");
            state.Append($"cell at {SEntMan.GetComponent<TransformComponent>(cell).Coordinates}");
            var crew = SEntMan.EntityQueryEnumerator<WFCrewComponent, TransformComponent>();
            while (crew.MoveNext(out var uid, out var member, out var at))
            {
                if (member.Group != ship.Group)
                    continue;
                var grabber = SEntMan.TryGetComponent<WFCrewGrabberComponent>(uid, out var job);
                state.Append($"; crew {member.Role} at {at.Coordinates} grabbing {grabber} swings {job?.Swings}");
                var hands = Server.System<Content.Shared.Hands.EntitySystems.SharedHandsSystem>();
                state.Append($" holds [{string.Join(",", hands.EnumerateHeld(uid).Select(item => SEntMan.ToPrettyString(item).ToString()))}]");
                var inventory = Server.System<Content.Shared.Inventory.InventorySystem>();
                foreach (var slot in new[] { "belt", "pocket1", "pocket2" })
                {
                    if (inventory.TryGetSlotEntity(uid, slot, out var worn))
                        state.Append($" {slot}={SEntMan.ToPrettyString(worn.Value)}");
                }
            }

            Assert.That(taken, Is.True, state.ToString());
        });

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<CuffableComponent>(captive).CuffedHandCount, Is.GreaterThan(0), "Cuffed.");
            Assert.That(SEntMan.GetComponent<MobStateComponent>(captive).CurrentState, Is.EqualTo(Content.Shared.Mobs.MobState.Alive), "Taken alive.");
            Assert.That(SEntMan.GetComponent<Content.Shared.Damage.DamageableComponent>(captive).TotalDamage.Float(), Is.LessThan(1f), "Taken unhurt.");
        });
    }

    /// <summary>The prisoner buys their way out from the chair, and goes home uncuffed with the money gone.</summary>
    [Test]
    public async Task PaidRansomSendsThePrisonerHome()
    {
        var (encounter, ship, abduct, cell) = await StartAbductor(new Vector2(-3000f, 3000f));
        EntityUid home = default;
        await Server.WaitAssertion(() =>
        {
            var mapSystem = Server.System<SharedMapSystem>();
            var grid = mapSystem.CreateGridEntity(MapData.MapId);
            home = grid.Owner;
            var tiles = new List<(Vector2i, Tile)>();
            for (var x = 0; x < 5; x++)
            for (var y = 0; y < 5; y++)
                tiles.Add((new Vector2i(x, y), new Tile(1)));
            mapSystem.SetTiles(grid, tiles);
            Server.System<SharedTransformSystem>().SetCoordinates(home, new EntityCoordinates(MapData.MapUid, new Vector2(-3000f, 3100f)));

            SEntMan.EnsureComponent<BankAccountComponent>(SPlayer);
            var bank = Server.System<BankSystem>();
            bank.TryGetBalance(SPlayer, out var balance);
            if (balance < AbductRansomAmount * 2)
                bank.TryBankDeposit(SPlayer, AbductRansomAmount * 2 - balance, false);

            // The test body is bare: it is given what a real prisoner's has.
            SEntMan.EnsureComponent<BuckleComponent>(SPlayer);
            Server.System<SharedTransformSystem>().SetCoordinates(SPlayer, SEntMan.GetComponent<TransformComponent>(cell).Coordinates);
            Assert.That(Server.System<Content.Shared.Buckle.SharedBuckleSystem>().TryBuckle(SPlayer, SPlayer, cell, popup: false), Is.True);
            abduct.Mark = home;
            abduct.Prisoner = SPlayer;
            abduct.Cell = cell;
            abduct.RansomEnds = Server.ResolveDependency<Robust.Shared.Timing.IGameTiming>().CurTime + TimeSpan.FromHours(1);
            abduct.Phase = WFAbductPhase.Ransom;
            Server.System<WFAbductCellSystem>().Hold((cell, SEntMan.GetComponent<WFAbductCellComponent>(cell)), SPlayer, AbductRansomAmount);
        });

        await Server.WaitAssertion(() =>
        {
            var verbs = Server.System<SharedVerbSystem>().GetLocalVerbs(cell, SPlayer, typeof(AlternativeVerb));
            var pay = verbs.SingleOrDefault(verb => verb.Text.Contains("5,000") || verb.Text.Contains("5000"));
            Assert.That(pay, Is.Not.Null, "The prisoner is offered the ransom from the chair.");
            var bank = Server.System<BankSystem>();
            bank.TryGetBalance(SPlayer, out var before);
            pay!.Act!();
            bank.TryGetBalance(SPlayer, out var after);
            Assert.That(before - after, Is.EqualTo(AbductRansomAmount), "The ransom came out of their account.");
            Assert.That(SEntMan.GetComponent<TransformComponent>(SPlayer).GridUid, Is.EqualTo(home), "Sent home.");
            Assert.That(SEntMan.GetComponent<BuckleComponent>(SPlayer).BuckledTo, Is.Null, "Out of the chair.");
            Assert.That(abduct.Phase, Is.EqualTo(WFAbductPhase.Roam), "The ship roams again.");
            Assert.That(abduct.Spared.ContainsKey(home), Is.True, "It leaves that ship alone a while.");
            Server.System<SharedTransformSystem>().SetCoordinates(SPlayer, new EntityCoordinates(MapData.MapUid, Vector2.Zero));
        });
    }

    /// <summary>
    /// From hands on someone to the ransom: the prisoner in the chair raises the warrant and bolts the door behind the
    /// crew, and with nobody paying they are sent home cuffs off when the ship's patience runs out.
    /// </summary>
    [Test]
    public async Task UnpaidPrisonerIsHeldThenSentHome()
    {
        await Server.WaitPost(() => EnsureBoard());
        var (encounter, ship, abduct, cell) = await StartAbductor(new Vector2(-3000f, -3000f));
        EntityUid captive = default;
        EntityUid home = default;
        await Server.WaitAssertion(() =>
        {
            var mapSystem = Server.System<SharedMapSystem>();
            var grid = mapSystem.CreateGridEntity(MapData.MapId);
            home = grid.Owner;
            var deck = new List<(Vector2i, Tile)>();
            for (var x = 0; x < 5; x++)
            for (var y = 0; y < 5; y++)
                deck.Add((new Vector2i(x, y), new Tile(1)));
            mapSystem.SetTiles(grid, deck);
            Server.System<SharedTransformSystem>().SetCoordinates(home, new EntityCoordinates(MapData.MapUid, new Vector2(-3000f, -2900f)));

            captive = SEntMan.SpawnEntity("MobHuman", Transform(ship.Grid, new Vector2(4.5f, -3.5f)));
            Server.System<SharedTransformSystem>().SetCoordinates(SPlayer, Transform(ship.Grid, new Vector2(4.5f, -3.5f)));
            abduct.Mark = home;
            abduct.Prisoner = captive;
            abduct.Cell = cell;
            abduct.Phase = WFAbductPhase.Grab;
            abduct.PhaseSince = Server.ResolveDependency<Robust.Shared.Timing.IGameTiming>().CurTime;
        });

        await WaitUntilServer(() => abduct.Phase == WFAbductPhase.Ransom, 6000);
        await Server.WaitAssertion(() =>
        {
            Assert.That(abduct.Taken, Is.EqualTo(1));
            Assert.That(abduct.RansomEnds, Is.Not.Null);
            Assert.That(Server.System<WFEncounterBountySystem>().Pool(encounter), Is.EqualTo(15000), "The ransom goes on the warrant.");
            Assert.That(SEntMan.GetComponent<WFAbductCellComponent>(cell).Prisoner, Is.EqualTo(captive));
        });

        // The hands walk out and the door is shut and bolted behind them.
        var lookup = Server.System<EntityLookupSystem>();
        bool Sealed()
        {
            var doors = new HashSet<Entity<AirlockComponent>>();
            lookup.GetEntitiesInRange(SEntMan.GetComponent<TransformComponent>(cell).Coordinates, 2.5f, doors);
            return doors.Count > 0 && doors.All(door => SEntMan.GetComponent<DoorComponent>(door).State == DoorState.Closed
                                                        && SEntMan.GetComponent<DoorBoltComponent>(door).BoltsDown);
        }

        await WaitUntilServer(Sealed, 3000);

        await Server.WaitPost(() => abduct.RansomEnds = Server.ResolveDependency<Robust.Shared.Timing.IGameTiming>().CurTime);
        await WaitUntilServer(() => abduct.Phase == WFAbductPhase.Roam, 300);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<TransformComponent>(captive).GridUid, Is.EqualTo(home), "Sent home.");
            Assert.That(SEntMan.GetComponent<CuffableComponent>(captive).CuffedHandCount, Is.Zero, "Cuffs off.");
            Assert.That(SEntMan.GetComponent<BuckleComponent>(captive).BuckledTo, Is.Null);
            var damage = SEntMan.GetComponent<Content.Shared.Damage.DamageableComponent>(captive).Damage.DamageDict
                .Where(pair => pair.Value > 0).Select(pair => $"{pair.Key}={pair.Value}");
            Assert.That(SEntMan.GetComponent<MobStateComponent>(captive).CurrentState, Is.EqualTo(Content.Shared.Mobs.MobState.Alive),
                string.Join(", ", damage));
            Assert.That(damage, Is.Empty, "Held unhurt, with nobody beating them in the chair.");
            Assert.That(abduct.Spared.ContainsKey(home), Is.True);
            Server.System<SharedTransformSystem>().SetCoordinates(SPlayer, new EntityCoordinates(MapData.MapUid, Vector2.Zero));
        });
    }

    /// <summary>With the crew dead the door stands open for a rescue, and the prisoner isn't deleted with the hull.</summary>
    [Test]
    public async Task DeadCrewLeaveTheCellDoorOpen()
    {
        var (encounter, ship, abduct, cell) = await StartAbductor(new Vector2(3000f, -3000f));
        await Server.WaitAssertion(() =>
        {
            abduct.Cell = cell;
            abduct.Prisoner = SPlayer;
            abduct.RansomEnds = Server.ResolveDependency<Robust.Shared.Timing.IGameTiming>().CurTime + TimeSpan.FromHours(1);
            abduct.Phase = WFAbductPhase.Ransom;
            Server.System<SharedTransformSystem>().SetCoordinates(SPlayer, SEntMan.GetComponent<TransformComponent>(cell).Coordinates);

            var crew = SEntMan.EntityQueryEnumerator<WFCrewComponent, MobStateComponent>();
            var mobs = Server.System<Content.Shared.Mobs.Systems.MobStateSystem>();
            while (crew.MoveNext(out var uid, out var member, out var state))
            {
                if (member.Group == ship.Group)
                    mobs.ChangeMobState(uid, Content.Shared.Mobs.MobState.Dead, state);
            }
        });

        await WaitUntilServer(() => abduct.Prisoner == null, 600);
        await Server.WaitAssertion(() =>
        {
            var lookup = Server.System<EntityLookupSystem>();
            var doors = new HashSet<Entity<AirlockComponent>>();
            lookup.GetEntitiesInRange(SEntMan.GetComponent<TransformComponent>(cell).Coordinates, 2.5f, doors);
            Assert.That(doors, Is.Not.Empty, "The cell has a door.");
            foreach (var door in doors)
            {
                Assert.That(SEntMan.GetComponent<DoorComponent>(door).State, Is.Not.EqualTo(DoorState.Closed), "Left open for a rescue.");
            }

            Server.System<SharedTransformSystem>().SetCoordinates(SPlayer, new EntityCoordinates(MapData.MapUid, Vector2.Zero));
        });
    }

    private EntityCoordinates Transform(EntityUid parent, Vector2 offset)
    {
        return new EntityCoordinates(parent, offset);
    }
}
