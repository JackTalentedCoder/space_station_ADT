using Content.Shared.ADT.Sanity;
using Content.Shared.Alert;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.ADT.Sanity;

/// <summary>
/// Базовый общий класс для системы рассудка.
/// Содержит методы, доступные как на сервере, так и на клиенте.
/// </summary>
public abstract class SharedSanitySystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;

    protected static readonly ProtoId<AlertPrototype> SanityAlertId = "HumanSanity";

    /// <summary>
    /// Изменяет рассудок сущности. На сервере – реальное изменение, на клиенте – ничего.
    /// </summary>
    public virtual void ModifySanity(EntityUid uid, float delta, SanityComponent? component = null)
    {
        // На клиенте ничего не делаем – логика только на сервере.
        if (EntityManager.IsClientSide(uid))
            return;
    }

    /// <summary>
    /// Устанавливает значение рассудка. На сервере – реальная установка.
    /// </summary>
    public virtual void SetSanity(EntityUid uid, float value, SanityComponent? component = null)
    {
        if (EntityManager.IsClientSide(uid))
            return;
    }

    /// <summary>
    /// Получает текущий рассудок. Доступно везде.
    /// </summary>
    public float GetSanity(EntityUid uid, SanityComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return 0f;
        return component.CurrentSanity;
    }

    /// <summary>
    /// Обновляет алерт рассудка по текущему значению.
    /// </summary>
    protected void UpdateAlert(EntityUid uid, SanityComponent component)
    {
        if (TerminatingOrDeleted(uid))
            return;

        var severity = (short)Math.Clamp(MathF.Round(component.CurrentSanity / 20f), 0, 5);
        _alerts.ShowAlert(uid, SanityAlertId, severity);
    }

    /// <summary>
    /// Обновляет состояние компонента Panophobia в зависимости от порога.
    /// </summary>
    protected void UpdatePanophobia(EntityUid uid, SanityComponent component)
    {
        if (component.CurrentSanity < component.SanityThreshold)
            EnsureComp<PanophobiaComponent>(uid);
        else
            RemComp<PanophobiaComponent>(uid);
    }
}