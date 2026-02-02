using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Physics.Components;

namespace Content.Shared.ADT.Infection.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class ContactInfectionComponent : Component
{
    /// <summary>
    /// Тип инфекции, которую наносит этот контакт
    /// </summary>
    [DataField]
    public InfectionType InfectionType = InfectionType.Both;

    /// <summary>
    /// Опциональный whitelist для игнора (как в DamageContacts)
    /// </summary>
    [DataField]
    public EntityWhitelist? IgnoreWhitelist;
}