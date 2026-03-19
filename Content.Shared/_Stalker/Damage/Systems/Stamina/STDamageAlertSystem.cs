using Content.Shared.Alert;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker.Damage.Systems;

public sealed class STDamageAlertSystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;

    private static readonly ProtoId<AlertPrototype> AcidAlert = "StalkerAcid";
    private static readonly ProtoId<AlertPrototype> RadAlert = "StalkerRad";
    private static readonly ProtoId<AlertPrototype> PsyAlert = "StalkerPsy";

    public override void Initialize()
    {
        SubscribeLocalEvent<DamageableComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<DamageableComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnStartup(Entity<DamageableComponent> ent, ref ComponentStartup args)
    {
        RefreshAlerts(ent.Owner, ent.Comp);
    }

    private void OnDamageChanged(Entity<DamageableComponent> ent, ref DamageChangedEvent args)
    {
        RefreshAlerts(ent.Owner, args.Damageable);
    }

    private void RefreshAlerts(EntityUid uid, DamageableComponent comp)
    {
        var total = comp.TotalDamage;

        if (total <= FixedPoint2.Zero)
        {
            _alerts.ClearAlert(uid, AcidAlert);
            _alerts.ClearAlert(uid, RadAlert);
            _alerts.ClearAlert(uid, PsyAlert);
            return;
        }

        comp.Damage.DamageDict.TryGetValue("Caustic", out var acid);
        comp.Damage.DamageDict.TryGetValue("Radiation", out var rad);
        comp.Damage.DamageDict.TryGetValue("Psy", out var psy);

        UpdateAlert(uid, AcidAlert, acid, total);
        UpdateAlert(uid, RadAlert, rad, total);
        UpdateAlert(uid, PsyAlert, psy, total);
    }

    private void UpdateAlert(EntityUid uid, ProtoId<AlertPrototype> alert, FixedPoint2 value, FixedPoint2 total)
    {
        if (value <= FixedPoint2.Zero)
        {
            _alerts.ClearAlert(uid, alert);
            return;
        }

        if (value >= total * 0.75f)
        {
            _alerts.ShowAlert(uid, alert, 2);
            return;
        }

        if (value >= total * 0.5f)
        {
            _alerts.ShowAlert(uid, alert, 1);
            return;
        }

        _alerts.ClearAlert(uid, alert);
    }
}