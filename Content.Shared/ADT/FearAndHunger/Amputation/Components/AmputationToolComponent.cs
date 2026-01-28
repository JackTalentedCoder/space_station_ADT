using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.ADT.Amputation.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AmputationToolComponent : Component
{
    [DataField, AutoNetworkedField]
    public AmputateLimb SelectedLimb { get; set; } = AmputateLimb.LeftLeg;

    [DataField]
    public SoundSpecifier SawSound = new SoundPathSpecifier("/Audio/ADT/Fear_and_Hunger/Effects/amputation.ogg");

    [DataField]
    public TimeSpan AmputationDelay = TimeSpan.FromSeconds(7);

    public EntityUid? CurrentSawingStream;
}