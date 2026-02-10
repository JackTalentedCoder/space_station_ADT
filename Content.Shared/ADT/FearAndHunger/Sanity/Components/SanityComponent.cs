using Content.Shared.Alert;
using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Generic;

namespace Content.Shared.ADT.Sanity.Components;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState(fieldDeltas: true), AutoGenerateComponentPause]
public sealed partial class SanityComponent : Component
{
    /// <summary>
    /// The sanity value as authoritatively set by the server as of <see cref="LastAuthoritativeSanityChangeTime"/>.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    [AutoNetworkedField]
    public float LastAuthoritativeSanityValue = 100.0f;

    /// <summary>
    /// The time at which <see cref="LastAuthoritativeSanityValue"/> was last updated.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public TimeSpan LastAuthoritativeSanityChangeTime;

    /// <summary>
    /// The base amount at which <see cref="LastAuthoritativeSanityValue"/> decays.
    /// </summary>
    [DataField("baseDecayRate"), ViewVariables(VVAccess.ReadWrite)]
    public float BaseDecayRate = 0.1f; // 1 единица каждые 10 секунд

    /// <summary>
    /// The actual amount at which <see cref="LastAuthoritativeSanityValue"/> decays.
    /// </summary>
    [DataField("actualDecayRate"), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public float ActualDecayRate = 0.1f;

    /// <summary>
    /// Alert prototype for displaying sanity
    /// </summary>
    [DataField]
    public ProtoId<AlertPrototype> SanityAlert = "HumanSanity";

    /// <summary>
    /// The time when the sanity will update next.
    /// </summary>
    [DataField("nextUpdateTime", customTypeSerializer: typeof(TimeOffsetSerializer)), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    [AutoPausedField]
    public TimeSpan NextUpdateTime;

    /// <summary>
    /// The time between each sanity update.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public TimeSpan UpdateRate = TimeSpan.FromSeconds(10); // Каждые 10 секунд

    /// <summary>
    /// Prevent component replication to clients other than the owner,
    /// doesn't affect prediction.
    /// </summary>
    public override bool SendOnlyToOwner => true;
}