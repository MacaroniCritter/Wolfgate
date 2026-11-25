using Content.Server.Atmos.Components;
using Content.Shared._WF.AtmosShield;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;

namespace Content.Server._WF.AtmosShield;

public sealed class WFAtmosShieldSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ItemToggleSystem _itemToggle = default!;

    private readonly Dictionary<EntityUid, EntityUid> _grantedPressureImmunity = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WFAtmosShieldComponent, ItemToggledEvent>(OnToggled);
        SubscribeLocalEvent<WFAtmosShieldComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<WFAtmosShieldComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<DamageableComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnToggled(Entity<WFAtmosShieldComponent> ent, ref ItemToggledEvent args)
    {
        if (args.Activated)
            GrantPressureImmunity(ent);
        else
            RemovePressureImmunity(ent);
    }

    private void OnMapInit(Entity<WFAtmosShieldComponent> ent, ref MapInitEvent args)
    {
        if (TryComp<ItemToggleComponent>(ent, out var toggle) && toggle.Activated)
            GrantPressureImmunity(ent);
    }

    private void OnShutdown(Entity<WFAtmosShieldComponent> ent, ref ComponentShutdown args)
    {
        RemovePressureImmunity(ent);
    }

    private void OnDamageChanged(Entity<DamageableComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.Origin is not { } origin)
            return;

        var wearer = TryComp<BodyPartComponent>(ent, out var part) && part.Body is { } body
            ? body
            : ent.Owner;

        if (origin == wearer ||
            !_inventory.TryGetSlotEntity(wearer, "outerClothing", out var suit) ||
            !HasComp<WFAtmosShieldComponent>(suit) ||
            !TryComp<ItemToggleComponent>(suit, out var toggle) ||
            !toggle.Activated)
        {
            return;
        }

        _itemToggle.TryDeactivate(suit.Value, predicted: false);
    }

    private void GrantPressureImmunity(Entity<WFAtmosShieldComponent> ent)
    {
        var uid = ent.Owner;
        if (_grantedPressureImmunity.ContainsKey(uid) ||
            !_inventory.InSlotWithFlags(uid, SlotFlags.OUTERCLOTHING) ||
            !_inventory.TryGetContainingEntity(uid, out var wearer) ||
            HasComp<PressureImmunityComponent>(wearer))
        {
            return;
        }

        EnsureComp<PressureImmunityComponent>(wearer.Value);
        _grantedPressureImmunity.Add(uid, wearer.Value);
    }

    private void RemovePressureImmunity(Entity<WFAtmosShieldComponent> ent)
    {
        if (!_grantedPressureImmunity.Remove(ent.Owner, out var wearer) || TerminatingOrDeleted(wearer))
            return;

        RemComp<PressureImmunityComponent>(wearer);
    }
}
