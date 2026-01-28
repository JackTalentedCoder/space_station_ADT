using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Physics.Components;

namespace Content.Shared.ADT.Infection.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class ContactInfectionComponent : Component
{
    /// <summary>
    /// Опциональный whitelist для игнора (как в DamageContacts)
    /// </summary>
    [DataField]
    public EntityWhitelist? IgnoreWhitelist;
}