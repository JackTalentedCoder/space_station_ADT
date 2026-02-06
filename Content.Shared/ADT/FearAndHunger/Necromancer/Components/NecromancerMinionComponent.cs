using Robust.Shared.GameStates;

namespace Content.Shared.ADT.Necromancer;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedNecromancerSystem))]
[AutoGenerateComponentState]
public sealed partial class NecromancerMinionComponent : Component
{
    /// <summary>
    /// Некромант, которому принадлежит этот миньон
    /// </summary>
    [DataField("necromancer")]
    [AutoNetworkedField]
    public EntityUid? Necromancer;

    /// <summary>
    /// Конструктор для копирования
    /// </summary>
    public NecromancerMinionComponent() { }

    public NecromancerMinionComponent(NecromancerMinionComponent other)
    {
        Necromancer = other.Necromancer;
    }
}