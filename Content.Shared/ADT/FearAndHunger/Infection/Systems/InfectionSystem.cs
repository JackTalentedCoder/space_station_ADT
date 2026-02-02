using Content.Shared.ADT.Infection.Components;
using Content.Shared.Alert;
using Content.Shared.Damage;
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

        // Подписки для инфекции рук
        SubscribeLocalEvent<HandInfectionComponent, ComponentInit>(OnHandInfectionInit);
        SubscribeLocalEvent<HandInfectionComponent, ComponentShutdown>(OnHandInfectionShutdown);

        // Подписки для инфекции ног
        SubscribeLocalEvent<LegInfectionComponent, ComponentInit>(OnLegInfectionInit);
        SubscribeLocalEvent<LegInfectionComponent, ComponentShutdown>(OnLegInfectionShutdown);
    }

    #region Обработчики для инфекции рук
    private void OnHandInfectionInit(Entity<HandInfectionComponent> ent, ref ComponentInit args)
    {
        ent.Comp.NextTick = _timing.CurTime + TimeSpan.FromSeconds(1f);
        ent.Comp.AlertStartTime = _timing.CurTime;

        ShowInfectionAlert(ent.Owner, ent.Comp.AlertId, ent.Comp.AlertStartTime, ent.Comp.InitialGracePeriod, ent.Comp.GraceActive);
    }

    private void OnHandInfectionShutdown(Entity<HandInfectionComponent> ent, ref ComponentShutdown args)
    {
        _alerts.ClearAlert(ent.Owner, ent.Comp.AlertId);
    }
    #endregion

    #region Обработчики для инфекции ног
    private void OnLegInfectionInit(Entity<LegInfectionComponent> ent, ref ComponentInit args)
    {
        ent.Comp.NextTick = _timing.CurTime + TimeSpan.FromSeconds(1f);
        ent.Comp.AlertStartTime = _timing.CurTime;

        ShowInfectionAlert(ent.Owner, ent.Comp.AlertId, ent.Comp.AlertStartTime, ent.Comp.InitialGracePeriod, ent.Comp.GraceActive);
    }

    private void OnLegInfectionShutdown(Entity<LegInfectionComponent> ent, ref ComponentShutdown args)
    {
        _alerts.ClearAlert(ent.Owner, ent.Comp.AlertId);
    }
    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        // Обработка инфекции рук
        UpdateHandInfections(curTime);

        // Обработка инфекции ног
        UpdateLegInfections(curTime);
    }

    private void UpdateHandInfections(TimeSpan curTime)
    {
        var query = EntityQueryEnumerator<HandInfectionComponent, DamageableComponent, MobStateComponent>();

        while (query.MoveNext(out var uid, out var infComp, out var damageable, out var mobState))
        {
            if (infComp.NextTick > curTime)
                continue;

            infComp.NextTick = curTime + TimeSpan.FromSeconds(1f);
            infComp.GracePeriod -= TimeSpan.FromSeconds(1f);

            // Когда grace истёк — убираем прогресс-бар
            if (infComp.GracePeriod <= TimeSpan.Zero && infComp.GraceActive)
            {
                infComp.GraceActive = false;
                ShowInfectionAlert(uid, infComp.AlertId, null, null, false);
            }

            if (infComp.GracePeriod <= TimeSpan.Zero)
            {
                _damageable.TryChangeDamage(uid, infComp.Damage, true, false, damageable);

                if (_mobState.IsDead(uid, mobState))
                {
                    // Опционально удалить компонент после смерти
                }
            }

            Dirty(uid, infComp);
        }
    }

    private void UpdateLegInfections(TimeSpan curTime)
    {
        var query = EntityQueryEnumerator<LegInfectionComponent, DamageableComponent, MobStateComponent>();

        while (query.MoveNext(out var uid, out var infComp, out var damageable, out var mobState))
        {
            if (infComp.NextTick > curTime)
                continue;

            infComp.NextTick = curTime + TimeSpan.FromSeconds(1f);
            infComp.GracePeriod -= TimeSpan.FromSeconds(1f);

            // Когда grace истёк — убираем прогресс-бар
            if (infComp.GracePeriod <= TimeSpan.Zero && infComp.GraceActive)
            {
                infComp.GraceActive = false;
                ShowInfectionAlert(uid, infComp.AlertId, null, null, false);
            }

            if (infComp.GracePeriod <= TimeSpan.Zero)
            {
                _damageable.TryChangeDamage(uid, infComp.Damage, true, false, damageable);

                if (_mobState.IsDead(uid, mobState))
                {
                    // Опционально удалить компонент после смерти
                }
            }

            Dirty(uid, infComp);
        }
    }

    private void ShowInfectionAlert(EntityUid uid, ProtoId<AlertPrototype> alertId, TimeSpan? start = null, TimeSpan? total = null, bool graceActive = true)
    {
        if (!TryComp<AlertsComponent>(uid, out var alerts))
            return;

        (TimeSpan, TimeSpan)? cooldown = null;
        if (start != null && total != null && graceActive)
        {
            cooldown = (start.Value, start.Value + total.Value);
        }

        _alerts.ShowAlert(uid, alertId, cooldown: cooldown);
    }
}