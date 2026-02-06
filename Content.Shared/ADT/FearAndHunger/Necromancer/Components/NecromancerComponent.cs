using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.ADT.Necromancer;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedNecromancerSystem))]
[AutoGenerateComponentState]
public sealed partial class NecromancerComponent : Component
{
    /// <summary>
    /// Действие для воскрешения миньона
    /// </summary>
    [DataField("actionRaiseMinion", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionRaiseMinion = "ADTActionNecromancy";

    /// <summary>
    /// Сущность действия для воскрешения миньона
    /// </summary>
    [DataField("actionRaiseMinionEntity")]
    public EntityUid? ActionRaiseMinionEntity;

    /// <summary>
    /// Время, необходимое для воскрешения (в секундах)
    /// </summary>
    [DataField("raiseDuration")]
    public float RaiseDuration = 5.0f;

    /// <summary>
    /// Действие для массового воскрешения
    /// </summary>
    [DataField("actionRaiseDead", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionRaiseDead = "ADTActionNecromancyRadius";

    /// <summary>
    /// Сущность действия для массового воскрешения
    /// </summary>
    [DataField("actionRaiseDeadEntity")]
    public EntityUid? ActionRaiseDeadEntity;

    /// <summary>
    /// Радиус массового воскрешения
    /// </summary>
    [DataField("raiseDeadRadius")]
    public float RaiseDeadRadius = 5.0f;

    /// <summary>
    /// Текущий приказ, который отдал Некромант
    /// </summary>
    [DataField("currentOrder"), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public NecromancerOrderType CurrentOrder = NecromancerOrderType.Follow;

    /// <summary>
    /// Миньоны, которыми в данный момент управляет Некромант
    /// </summary>
    [DataField("minions")]
    public HashSet<EntityUid> Minions = new();

    /// <summary>
    /// Действие приказа "Остаться"
    /// </summary>
    [DataField("actionOrderStay", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionOrderStay = "ADTActionNecromancyOrderStay";

    /// <summary>
    /// Сущность действия приказа "Остаться"
    /// </summary>
    [DataField("actionOrderStayEntity")]
    public EntityUid? ActionOrderStayEntity;

    /// <summary>
    /// Действие приказа "Следовать"
    /// </summary>
    [DataField("actionOrderFollow", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionOrderFollow = "ADTActionNecromancyOrderFollow";

    /// <summary>
    /// Сущность действия приказа "Следовать"
    /// </summary>
    [DataField("actionOrderFollowEntity")]
    public EntityUid? ActionOrderFollowEntity;

    /// <summary>
    /// Действие приказа "Атаковать"
    /// </summary>
    [DataField("actionOrderAttack", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionOrderAttack = "ADTActionNecromancyOrderAttack";

    /// <summary>
    /// Сущность действия приказа "Атаковать"
    /// </summary>
    [DataField("actionOrderAttackEntity")]
    public EntityUid? ActionOrderAttackEntity;

    /// <summary>
    /// Действие приказа "Свободно"
    /// </summary>
    [DataField("actionOrderLoose", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ActionOrderLoose = "ADTActionNecromancyOrderLoose";

    /// <summary>
    /// Сущность действия приказа "Свободно"
    /// </summary>
    [DataField("actionOrderLooseEntity")]
    public EntityUid? ActionOrderLooseEntity;

    /// <summary>
    /// Словарь приказов и соответствующих им криков/команд
    /// </summary>
    [DataField("orderCallouts")]
    public Dictionary<NecromancerOrderType, string> OrderCallouts = new()
    {
        { NecromancerOrderType.Stay, "NecromancerCommandStay" },
        { NecromancerOrderType.Follow, "NecromancerCommandFollow" },
        { NecromancerOrderType.Attack, "NecromancerCommandAttack" },
        { NecromancerOrderType.Loose, "NecromancerCommandLoose" }
    };
}

[Serializable, NetSerializable]
public enum NecromancerOrderType : byte
{
    Stay,
    Follow,
    Attack,
    Loose
}