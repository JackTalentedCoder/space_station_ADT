using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.ADT.Necromancer;

/// <summary>
/// Компонент, который помечает сущность как доступную для воскрешения некромантом
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NecromancyAvailableComponent : Component
{
    /// <summary>
    /// Был ли уже воскрешен этот труп
    /// </summary>
    [DataField("raised")]
    public bool Raised = false;

    /// <summary>
    /// Прототип существа, в которое превратится труп
    /// </summary>
    [DataField("minionPrototype")]
    public string MinionPrototype = "ADTNecromancyGhoul"; // Укажите ваш прототип по умолчанию

    /// <summary>
    /// Длительность воскрешения в секундах
    /// </summary>
    [DataField("raiseDuration")]
    public float RaiseDuration = 7.0f;
}