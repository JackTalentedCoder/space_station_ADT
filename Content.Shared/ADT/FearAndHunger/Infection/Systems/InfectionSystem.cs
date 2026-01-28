using Content.Shared.ADT.Infection.Components;
using Content.Shared.Alert;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;


namespace Content.Shared.ADT.Infection;

public sealed class InfectionSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PendingInfectionComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<PendingInfectionComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnInit(Entity<PendingInfectionComponent> ent, ref ComponentInit args)
    {
        ent.Comp.NextTick = _timing.CurTime + TimeSpan.FromSeconds(1f);
        ent.Comp.AlertStartTime = _timing.CurTime;

        // Фиксированный cooldown на всю начальную длительность grace
        ShowInfectionAlert(ent.Owner, ent.Comp);
    }

    private void OnShutdown(Entity<PendingInfectionComponent> ent, ref ComponentShutdown args)
    {
        _alerts.ClearAlert(ent.Owner, ent.Comp.AlertId);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<PendingInfectionComponent, DamageableComponent, MobStateComponent>();

        while (query.MoveNext(out var uid, out var infComp, out var damageable, out var mobState))
        {
            if (infComp.NextTick > curTime)
                continue;

            infComp.NextTick = curTime + TimeSpan.FromSeconds(1f);

            infComp.GracePeriod -= TimeSpan.FromSeconds(1f);

            // Когда grace истёк — убираем прогресс-бар (оставляем просто иконку инфекции)
            if (infComp.GracePeriod <= TimeSpan.Zero && infComp.GraceActive)
            {
                infComp.GraceActive = false;
                ShowInfectionAlert(uid, infComp); // без cooldown
            }

            if (infComp.GracePeriod <= TimeSpan.Zero)
            {
                _damageable.TryChangeDamage(uid, infComp.Damage, true, false, damageable);

                if (_mobState.IsDead(uid, mobState))
                {
                    // Опционально удалить компонент после смерти
                    // RemComp<PendingInfectionComponent>(uid);
                }
            }

            Dirty(uid, infComp);
        }
    }

    private void ShowInfectionAlert(EntityUid uid, PendingInfectionComponent comp)
    {
        if (!TryComp<AlertsComponent>(uid, out var alerts))
            return;

        // Фиксированный старт + полная начальная длительность
        var start = comp.AlertStartTime;
        var total = comp.InitialGracePeriod;

        (TimeSpan, TimeSpan)? cooldown = comp.GraceActive
            ? (start, start + total)
            : null;

        _alerts.ShowAlert(uid, comp.AlertId, cooldown: cooldown);
    }
}