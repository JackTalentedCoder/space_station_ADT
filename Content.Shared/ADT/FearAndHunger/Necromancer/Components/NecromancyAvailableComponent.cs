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
    /// Прототип существа, в которое превратится труп при воскрешении
    /// Если null, то воскрешается та же самая сущность
    /// </summary>
    [DataField("minionPrototype")]
    public string? MinionPrototype = null;

    /// <summary>
    /// Длительность воскрешения в секундах
    /// </summary>
    [DataField("raiseDuration")]
    public float RaiseDuration = 8.0f;

    /// <summary>
    /// Звук, который воспроизводится при воскрешении
    /// </summary>
    [DataField("raiseSound")]
    public SoundSpecifier? RaiseSound;

    /// <summary>
    /// Сохранять ли компонент Hands при превращении
    /// </summary>
    [DataField("keepHands")]
    public bool KeepHands = false;
}