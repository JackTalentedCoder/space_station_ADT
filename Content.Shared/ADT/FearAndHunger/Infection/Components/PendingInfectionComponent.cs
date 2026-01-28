using Content.Shared.Alert;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.ADT.Infection.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PendingInfectionComponent : Component
{
    /// <summary>
    /// Начальная длительность grace period (для фиксированного алерта)
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan InitialGracePeriod = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Оставшееся время grace period
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan GracePeriod = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Время старта алерта (для фиксированного cooldown)
    /// </summary>
    [DataField]
    public TimeSpan AlertStartTime;

    /// <summary>
    /// Флаг, что grace ещё активен (для переключения на статичный алерт после истечения)
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool GraceActive = true;

    /// <summary>
    /// Следующий тик обработки
    /// </summary>
    [DataField]
    public TimeSpan NextTick;

    /// <summary>
    /// Урон Asphyxiation 10/сек после grace
    /// </summary>
    [DataField]
    public DamageSpecifier Damage = new()
    {
        DamageDict = { { "Asphyxiation", 10 } }
    };

    /// <summary>
    /// ID алерта
    /// </summary>
    [DataField(required: true)]
    public ProtoId<AlertPrototype> AlertId = "ADTAlertInfected";
}