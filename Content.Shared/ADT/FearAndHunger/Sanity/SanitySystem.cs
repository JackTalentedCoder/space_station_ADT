using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.ADT.Sanity.Components;
using Content.Shared.Alert;
using Content.Shared.Damage;
using Content.Shared.Mobs.Systems;
using Content.Shared.Rejuvenate;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared.ADT.Sanity.EntitySystems;

public sealed class SanitySystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SanityComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SanityComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SanityComponent, RejuvenateEvent>(OnRejuvenate);

        // Подписываемся на изменение урона для применения модификатора Panophobia
        SubscribeLocalEvent<PanophobiaComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnMapInit(EntityUid uid, SanityComponent component, MapInitEvent args)
    {
        // Начинаем со 100% рассудка
        SetSanity(uid, 100.0f, component);
        component.NextUpdateTime = _timing.CurTime + component.UpdateRate;

        // Показываем алерт
        UpdateSanityAlert(uid, component);
    }

    private void OnShutdown(EntityUid uid, SanityComponent component, ComponentShutdown args)
    {
        _alerts.ClearAlert(uid, component.SanityAlert);
    }

    private void OnRejuvenate(EntityUid uid, SanityComponent component, RejuvenateEvent args)
    {
        SetSanity(uid, 100.0f, component);
    }

    /// <summary>
    /// Gets the current sanity value of the given <see cref="SanityComponent"/>.
    /// </summary>
    public float GetSanity(SanityComponent component)
    {
        var dt = _timing.CurTime - component.LastAuthoritativeSanityChangeTime;
        var value = component.LastAuthoritativeSanityValue - (float)dt.TotalSeconds * component.ActualDecayRate;
        return ClampSanity(value);
    }

    /// <summary>
    /// Adds to the current sanity of an entity by the specified value
    /// </summary>
    public void ModifySanity(EntityUid uid, float amount, SanityComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;
        SetSanity(uid, GetSanity(component) + amount, component);
    }

    /// <summary>
    /// Sets the current sanity of an entity to the specified value
    /// </summary>
    public void SetSanity(EntityUid uid, float amount, SanityComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        SetAuthoritativeSanityValue((uid, component), amount);
        UpdateSanityAlert(uid, component);
        UpdatePanophobia(uid, component);
    }

    /// <summary>
    /// Sets authoritative sanity values
    /// </summary>
    private void SetAuthoritativeSanityValue(Entity<SanityComponent> entity, float value)
    {
        entity.Comp.LastAuthoritativeSanityChangeTime = _timing.CurTime;
        entity.Comp.LastAuthoritativeSanityValue = ClampSanity(value);
        DirtyField(entity.Owner, entity.Comp, nameof(SanityComponent.LastAuthoritativeSanityChangeTime));
        DirtyField(entity.Owner, entity.Comp, nameof(SanityComponent.LastAuthoritativeSanityValue));
    }

    /// <summary>
    /// Updates the sanity alert with appropriate severity
    /// </summary>
    private void UpdateSanityAlert(EntityUid uid, SanityComponent component)
    {
        var sanity = GetSanity(component);

        // Конвертируем проценты (0-100) в severity (0-5)
        // 0% = severity 0, 100% = severity 5
        var severity = (short)Math.Round(sanity / 20.0f);
        severity = Math.Clamp(severity, (short)0, (short)5);

        _alerts.ShowAlert(uid, component.SanityAlert, severity);
    }

    /// <summary>
    /// Updates Panophobia component based on sanity level
    /// </summary>
    private void UpdatePanophobia(EntityUid uid, SanityComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var sanity = GetSanity(component);

        if (sanity < 15.0f) // Меньше 15% рассудка
        {
            EnsureComp<PanophobiaComponent>(uid);
        }
        else
        {
            RemComp<PanophobiaComponent>(uid);
        }
    }

    /// <summary>
    /// Applies damage multiplier from Panophobia - УВЕЛИЧИВАЕТ любой входящий урон на 50%
    /// </summary>
    private void OnDamageModify(EntityUid uid, PanophobiaComponent component, DamageModifyEvent args)
    {
        // Умножаем ВЕСЬ урон на множитель 1.5 (увеличение на 50%)
        foreach (var damageType in args.Damage.DamageDict.Keys.ToList())
        {
            args.Damage.DamageDict[damageType] *= component.DamageMultiplier;
        }
    }

    /// <summary>
    /// Gets the sanity threshold for displaying alerts (not used for gradient alerts)
    /// </summary>
    public float GetSanityPercent(SanityComponent component, float? sanity = null)
    {
        sanity ??= GetSanity(component);
        return sanity.Value;
    }

    /// <summary>
    /// Checks if entity sanity is below specified percentage
    /// </summary>
    public bool IsSanityBelow(EntityUid uid, float percentage, float? sanity = null, SanityComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false;

        var currentSanity = sanity ?? GetSanity(comp);
        return currentSanity < percentage;
    }

    private static float ClampSanity(float sanityValue)
    {
        return Math.Clamp(sanityValue, 0.0f, 100.0f);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SanityComponent>();
        while (query.MoveNext(out var uid, out var sanity))
        {
            if (_timing.CurTime < sanity.NextUpdateTime)
                continue;

            sanity.NextUpdateTime = _timing.CurTime + sanity.UpdateRate;

            // Уменьшаем рассудок на 1 единицу
            ModifySanity(uid, -1.0f, sanity);
        }
    }
}