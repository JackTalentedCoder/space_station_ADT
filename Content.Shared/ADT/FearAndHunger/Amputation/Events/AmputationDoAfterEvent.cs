using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.ADT.Amputation;

[Serializable, NetSerializable]
public sealed partial class AmputationDoAfterEvent : DoAfterEvent
{
    public AmputateLimb Limb { get; set; }

    public AmputationDoAfterEvent(AmputateLimb limb)
    {
        Limb = limb;
    }

    public override DoAfterEvent Clone() => new AmputationDoAfterEvent(Limb);
}