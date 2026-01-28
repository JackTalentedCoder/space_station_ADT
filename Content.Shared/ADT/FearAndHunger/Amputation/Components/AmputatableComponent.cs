using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.ADT.Amputation.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class AmputatableComponent : Component
{
    [DataField] public EntProtoId? SeveredHead;
    [DataField] public EntProtoId? SeveredLeftArm;
    [DataField] public EntProtoId? SeveredRightArm;
    [DataField] public EntProtoId? SeveredLeftLeg;
    [DataField] public EntProtoId? SeveredRightLeg;
}