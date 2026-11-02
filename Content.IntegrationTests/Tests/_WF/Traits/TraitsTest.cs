#nullable enable
using System.Linq;
using Content.Server.Traits;
using Content.Shared._WF.Traits;
using Content.Shared._WF.Traits.Conditions;
using Content.Shared._WF.Traits.Effects;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Hands.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Preferences;
using Content.Shared.Traits;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._WF.Traits;

/// <summary>
/// Data-driven trait conditions and effects parse, are checked against the spawn, and apply to the character.
/// </summary>
[TestFixture]
public sealed class TraitsTest
{
    [TestPrototypes]
    private const string Prototypes = """
                                      - type: traitCategory
                                        id: WFTestTraits
                                        name: trait-category-quirks

                                      - type: trait
                                        id: WFTestConditions
                                        name: trait-pacifist-name
                                        description: trait-pacifist-desc
                                        category: WFTestTraits
                                        cost: 0
                                        conditions:
                                        - !type:HasJobCondition
                                          job: PrisonGuard
                                        - !type:InDepartmentCondition
                                          department: Security
                                        - !type:IsSpeciesCondition
                                          species: Human
                                        - !type:HasCompCondition
                                          component: HumanoidAppearance
                                        - !type:HasJobCondition
                                          job: Captain
                                          invert: true
                                        effects:
                                        - !type:AddCompsEffect
                                          components:
                                          - type: Pacified
                                        - !type:RemCompsEffect
                                          components:
                                          - Pacified
                                        - !type:SpawnItemInHandEffect
                                          item: WhiteCane

                                      - type: trait
                                        id: WFTestOverride
                                        name: trait-pacifist-name
                                        description: trait-pacifist-desc
                                        category: WFTestTraits
                                        cost: 0
                                        effects:
                                        - !type:OverrideCompsEffect
                                          components:
                                          - type: MovementSpeedModifier
                                            baseWalkSpeed: 1.23

                                      - type: trait
                                        id: WFTestMothOnly
                                        name: trait-pacifist-name
                                        description: trait-pacifist-desc
                                        category: WFTestTraits
                                        cost: 0
                                        conditions:
                                        - !type:IsSpeciesCondition
                                          species: Moth
                                        effects:
                                        - !type:AddCompsEffect
                                          components:
                                          - type: Pacified
                                      """;

    [Test]
    public async Task ConditionsAndEffectsWork()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var protoMan = server.ProtoMan;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var conditionsTrait = protoMan.Index<TraitPrototype>("WFTestConditions");
            var overrideTrait = protoMan.Index<TraitPrototype>("WFTestOverride");

            Assert.That(conditionsTrait.Conditions, Has.Count.EqualTo(5));
            Assert.That(conditionsTrait.Conditions[0], Is.TypeOf<HasJobCondition>());
            Assert.That(conditionsTrait.Conditions[1], Is.TypeOf<InDepartmentCondition>());
            Assert.That(conditionsTrait.Conditions[2], Is.TypeOf<IsSpeciesCondition>());
            Assert.That(conditionsTrait.Conditions[3], Is.TypeOf<HasCompCondition>());
            Assert.That(conditionsTrait.Effects, Has.Count.EqualTo(3));
            Assert.That(conditionsTrait.Effects[0], Is.TypeOf<AddCompsEffect>());
            Assert.That(conditionsTrait.Effects[1], Is.TypeOf<RemCompsEffect>());
            Assert.That(conditionsTrait.Effects[2], Is.TypeOf<SpawnItemInHandEffect>());

            var mob = entMan.SpawnEntity("MobHuman", map.MapCoords);
            var traitSystem = entMan.System<TraitSystem>();

            Assert.That(traitSystem.WfTraitConditionsMet(mob, "PrisonGuard", conditionsTrait), Is.True,
                "PrisonGuard in Security as a Human meets every condition.");
            Assert.That(traitSystem.WfTraitConditionsMet(mob, "Captain", conditionsTrait), Is.False,
                "A Captain fails the job, department and inverted job conditions.");

            var ctx = new TraitConditionContext
            {
                Player = mob,
                JobId = "PrisonGuard",
                SpeciesId = "Human",
                EntMan = entMan,
                Proto = protoMan,
                CompFactory = entMan.ComponentFactory,
            };

            foreach (var condition in conditionsTrait.Conditions)
            {
                Assert.That(condition.Evaluate(ctx), Is.True, condition.GetType().Name);
            }

            var inverted = conditionsTrait.Conditions[4];
            ctx.JobId = "Captain";
            Assert.That(inverted.Evaluate(ctx), Is.False, "invert flips a passing condition.");
            ctx.JobId = "PrisonGuard";

            // Unknown data passes so the editor never blocks a condition it cannot see yet.
            var preview = new TraitConditionContext { SpeciesId = "Human", Proto = protoMan };
            Assert.That(TraitConditionEvaluator.AllMet(conditionsTrait.Conditions, preview), Is.True);

            traitSystem.WfApplyTraitEffects(mob, conditionsTrait);

            Assert.That(entMan.HasComponent<PacifiedComponent>(mob), Is.False,
                "RemCompsEffect runs after AddCompsEffect.");
            Assert.That(entMan.TryGetComponent<HandsComponent>(mob, out var hands));
            var held = hands!.Hands.Values.Select(hand => hand.HeldEntity).FirstOrDefault(entity => entity != null);
            Assert.That(held, Is.Not.Null, "SpawnItemInHandEffect puts the item in a hand.");
            Assert.That(entMan.GetComponent<MetaDataComponent>(held!.Value).EntityPrototype?.ID, Is.EqualTo("WhiteCane"));

            traitSystem.WfApplyTraitEffects(mob, overrideTrait);
            var movement = entMan.GetComponent<MovementSpeedModifierComponent>(mob);
            Assert.That(movement.BaseWalkSpeed, Is.EqualTo(1.23f),
                "OverrideCompsEffect replaces the existing component.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ProfileDropsTraitsWithUnmetConditions()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var profile = HumanoidCharacterProfile.DefaultWithSpecies();
            var traits = new ProtoId<TraitPrototype>[]
            {
                "WFTestConditions",
                "WFTestMothOnly",
                "Pacifist",
            };

            Assert.That(profile.GetValidTraits(traits, server.ProtoMan),
                Is.EquivalentTo(new ProtoId<TraitPrototype>[] { "WFTestConditions", "Pacifist" }));
        });

        await pair.CleanReturnAsync();
    }
}
