using Content.Shared.Actions;
using Content.Shared.ADT.Necromancer;

namespace Content.Shared.Necromancer;

public sealed partial class NecromancerRaiseMinionActionEvent : InstantActionEvent
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