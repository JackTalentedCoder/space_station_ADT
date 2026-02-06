using Content.Shared.Actions;
using Content.Shared.ADT.Necromancer;
using Robust.Shared.Serialization;

namespace Content.Shared.Necromancer;

public sealed partial class NecromancerRaiseMinionActionEvent : EntityTargetActionEvent
{
}

public sealed partial class NecromancerOrderActionEvent : InstantActionEvent
{
    /// <summary>
    /// Тип приказа, который отдается
    /// </summary>
    [DataField("type")]
    public NecromancerOrderType Type;
}

public sealed partial class NecromancerRaiseDeadActionEvent : InstantActionEvent
{
}

[Serializable, NetSerializable]
public sealed partial class NecromancerRaiseMinionDoAfterEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public sealed partial class NecromancerRaiseDeadDoAfterEvent : SimpleDoAfterEvent
{
}