using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.ADT.Sanity;

/// <summary>
/// Хранит текущее значение рассудка, максимальное значение, порог для панофобии и расписание спада.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SanityComponent : Component
{
    /// <summary>
    /// Текущий рассудок (0–100).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float CurrentSanity = 100f;

    /// <summary>
    /// Максимальное значение рассудка (не изменяется).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MaxSanity = 100f;

    /// <summary>
    /// Порог, ниже которого добавляется компонент Panophobia.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SanityThreshold = 15f;

    /// <summary>
    /// Время следующего планового уменьшения рассудка.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan NextDecayTime;

    /// <summary>
    /// Интервал между уменьшениями (10 секунд).
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan DecayInterval = TimeSpan.FromSeconds(10);
}