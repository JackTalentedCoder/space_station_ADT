using Robust.Shared.GameStates;
using Content.Shared.ADT.Amputation;

namespace Content.Shared.ADT.Amputation.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AmputatedLimbsComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<AmputateLimb> Amputated { get; set; } = new();
}