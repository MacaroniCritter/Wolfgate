using Content.Shared._WG.WallPaint;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.SprayPainter.Components;
using Robust.Shared.Audio.Systems;

namespace Content.Server._WG.WallPaint;

public sealed partial class WGWallPaintSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WGPaintableWallComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<WGPaintableWallComponent, InteractUsingEvent>(OnWallInteract);
        SubscribeLocalEvent<SprayPainterComponent, WGWallPaintDoAfterEvent>(OnPaintDoAfter);
    }

    private void OnStartup(Entity<WGPaintableWallComponent> ent, ref ComponentStartup args)
    {
        UpdateAppearance(ent);
    }

    private void OnWallInteract(Entity<WGPaintableWallComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled ||
            !TryComp<SprayPainterComponent>(args.Used, out var painter) ||
            painter.PickedColor is not { } key ||
            !painter.ColorPalette.TryGetValue(key, out var color))
        {
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(1),
            new WGWallPaintDoAfterEvent(color), args.Used, target: ent, used: args.Used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        args.Handled = _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnPaintDoAfter(Entity<SprayPainterComponent> ent, ref WGWallPaintDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Target is not { } target ||
            !TryComp(target, out WGPaintableWallComponent? paintable))
        {
            return;
        }

        PaintWall((target, paintable), args.Color);
        _audio.PlayPvs(ent.Comp.SpraySound, ent);
        args.Handled = true;
    }

    /// <summary>Sets the wall's persistent paint and updates its networked appearance.</summary>
    public void PaintWall(Entity<WGPaintableWallComponent> ent, Color color)
    {
        ent.Comp.Color = color;
        ent.Comp.Painted = true;
        Dirty(ent, ent.Comp);
        UpdateAppearance(ent);
    }

    private void UpdateAppearance(Entity<WGPaintableWallComponent> ent)
    {
        var appearance = EnsureComp<AppearanceComponent>(ent.Owner);
        _appearance.SetData(ent.Owner, WGWallPaintVisuals.Color, ent.Comp.Color, appearance);
        _appearance.SetData(ent.Owner, WGWallPaintVisuals.Painted, ent.Comp.Painted, appearance);
    }
}
