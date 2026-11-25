using System.Numerics;
using Content.Shared._Mono.PersonalShield;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle.Components; // WOLFGATE(AtmosShield)
using Content.Shared._WF.AtmosShield; // WOLFGATE(AtmosShield)
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Mono.PersonalShield;

public sealed partial class PersonalShieldOverlay : Overlay
{
    [Dependency] private IEntityManager _entManager = null!;
    [Dependency] private IGameTiming _timing = null!;

    private static readonly ProtoId<ShaderPrototype> ShaderId = "PersonalShieldSkin";

    private readonly SharedTransformSystem _transform;
    private readonly SpriteSystem _sprite;
    private readonly InventorySystem _inventory;
    private readonly ShaderPrototype _shaderProto;

    private readonly Dictionary<EntityUid, ShaderInstance> _shaders = new();
    private readonly List<EntityUid> _stale = new();

    // WOLFGATE(AtmosShield) START: render the suit with the Medusa bubble-shield effect.
    private static readonly PersonalShieldVisuals AtmosShieldVisuals = new()
    {
        Color = Color.FromHex("#BCF3FF"),
        FillLevel = 0.2f,
        HexDensity = 4f,
        PixelGrid = 192f,
        Scale = 1.6f,
    };
    // WOLFGATE END

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public PersonalShieldOverlay()
    {
        IoCManager.InjectDependencies(this);
        _transform = _entManager.System<SharedTransformSystem>();
        _sprite = _entManager.System<SpriteSystem>();
        _inventory = _entManager.System<InventorySystem>();
        var protoMan = IoCManager.Resolve<IPrototypeManager>();
        _shaderProto = protoMan.Index(ShaderId);
    }

    protected override void DisposeBehavior()
    {
        base.DisposeBehavior();

        foreach (var shader in _shaders.Values)
        {
            shader.Dispose();
        }

        _shaders.Clear();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        // WOLFGATE(AtmosShield) START: draw the atmospheric suit with the Medusa bubble renderer.
        if (args.MapId == MapId.Nullspace)
            return;

        var handle = args.WorldHandle;

        // Cancel the eye rotation so the shield is always "upright".
        var eyeRot = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var counterRot = Matrix3Helpers.CreateRotation(-eyeRot);

        var query = _entManager.EntityQueryEnumerator<PersonalShieldComponent>();
        while (query.MoveNext(out var uid, out var shield))
        {
            if (shield.Runtime.Form <= 0f && shield.Runtime.Shatter <= 0f)
                continue;

            if (!_inventory.TryGetContainingEntity(uid, out var wearer))
                continue;

            var flare = GetDamageFlare(shield);
            var charge = MathF.Max(shield.Runtime.Charge / MathF.Max(shield.Shield.MaxCharge, 0.001f), 0f);
            var minimum = Math.Clamp(shield.Visuals.MinimumBrightness, 0f, 1f);
            var brightness = charge > 1f ? charge : minimum + (1f - minimum) * charge;
            brightness += MathF.Max(1f - brightness, 0f) * flare;

            DrawBubble(uid, wearer.Value, args, counterRot, shield.Visuals, GetProgress(shield),
                shield.Visuals.Brightness * brightness, flare);
        }

        var atmosQuery = _entManager.EntityQueryEnumerator<WFAtmosShieldComponent, ItemToggleComponent>();
        while (atmosQuery.MoveNext(out var uid, out _, out var toggle))
        {
            if (!toggle.Activated ||
                !_inventory.InSlotWithFlags(uid, SlotFlags.OUTERCLOTHING) ||
                !_inventory.TryGetContainingEntity(uid, out var wearer))
            {
                continue;
            }

            DrawBubble(uid, wearer.Value, args, counterRot, AtmosShieldVisuals, 1f, 1f, 0f);
        }

        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(null);

        PruneShaders();
        // WOLFGATE END
    }

    // WOLFGATE(AtmosShield) START: share the existing personal-shield bubble rendering with the atmos suit.
    private void DrawBubble(
        EntityUid shieldUid,
        EntityUid wearer,
        in OverlayDrawArgs args,
        Matrix3x2 counterRot,
        PersonalShieldVisuals visuals,
        float progress,
        float brightness,
        float flare)
    {
        if (!_entManager.TryGetComponent(wearer, out SpriteComponent? sprite) || !sprite.Visible ||
            !_entManager.TryGetComponent(wearer, out TransformComponent? xform) || xform.MapID != args.MapId ||
            !TryGetHitboxSize(wearer, sprite, out var extents))
        {
            return;
        }

        var shader = GetShader(shieldUid);
        shader.SetParameter("progress", progress);
        shader.SetParameter("skin_color", visuals.Color);
        shader.SetParameter("brightness", brightness);
        shader.SetParameter("pixel_grid", visuals.PixelGrid);
        shader.SetParameter("hex_density", visuals.HexDensity);
        shader.SetParameter("form_origin", visuals.FormOrigin);
        shader.SetParameter("fill_level", visuals.FillLevel);
        shader.SetParameter("line_level", visuals.LineLevel);
        shader.SetParameter("rim_level", visuals.RimLevel);
        shader.SetParameter("core_fade", visuals.CoreFade);
        shader.SetParameter("shard_scale", visuals.ShardScale);
        shader.SetParameter("alpha_bands", visuals.AlphaBands);
        shader.SetParameter("breath_depth", visuals.BreathDepth);
        shader.SetParameter("damage_flare", flare);

        args.WorldHandle.UseShader(shader);
        var worldPos = _transform.GetWorldPosition(xform);
        args.WorldHandle.SetTransform(Matrix3x2.Multiply(counterRot, Matrix3Helpers.CreateTranslation(worldPos)));
        args.WorldHandle.DrawTextureRect(Texture.White,
            Box2.CenteredAround(Vector2.Zero, extents * visuals.Scale));
    }
    // WOLFGATE END

    private void PruneShaders()
    {
        foreach (var (uid, shader) in _shaders)
        {
            if (_entManager.HasComponent<PersonalShieldComponent>(uid) ||
                _entManager.HasComponent<WFAtmosShieldComponent>(uid)) // WOLFGATE(AtmosShield)
                continue;

            shader.Dispose();
            _stale.Add(uid);
        }

        foreach (var uid in _stale)
        {
            _shaders.Remove(uid);
        }

        _stale.Clear();
    }

    private ShaderInstance GetShader(EntityUid uid)
    {
        if (!_shaders.TryGetValue(uid, out var shader))
        {
            shader = _shaderProto.InstanceUnique();
            _shaders[uid] = shader;
        }

        return shader;
    }

    private bool TryGetHitboxSize(EntityUid uid, SpriteComponent sprite, out Vector2 extents)
    {
        extents = Vector2.Zero;

        if (_entManager.TryGetComponent(uid, out FixturesComponent? fixtures) && fixtures.FixtureCount > 0)
        {
            var identity = new Transform(Vector2.Zero, 0f);
            Box2? union = null;

            foreach (var fixture in fixtures.Fixtures.Values)
            {
                if (!fixture.Hard)
                    continue;

                var aabb = fixture.Shape.ComputeAABB(identity, 0);
                union = union?.Union(aabb) ?? aabb;
            }

            if (union is { } box && box.Width > 0f && box.Height > 0f)
            {
                extents = box.Size;
                return true;
            }
        }

        var bounds = _sprite.GetLocalBounds((uid, sprite));
        extents = bounds.Size;
        return extents is { X: > 0f, Y: > 0f };
    }

    private float GetDamageFlare(PersonalShieldComponent shield)
    {
        if (shield.Visuals.DamageFlareTime <= 0f)
            return 0f;

        var remaining = (float) (shield.Runtime.DamageFlareUntil - _timing.CurTime).TotalSeconds;
        return Math.Clamp(remaining / shield.Visuals.DamageFlareTime, 0f, 1f);
    }

    private static float GetProgress(PersonalShieldComponent shield)
    {
        return shield.Runtime.Shatter > 0f
            ? 1f + MathF.Min(shield.Runtime.Shatter, 1f)
            : shield.Runtime.Form;
    }
}
