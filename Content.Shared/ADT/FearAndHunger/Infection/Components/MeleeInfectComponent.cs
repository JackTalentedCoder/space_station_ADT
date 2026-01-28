using Robust.Shared.GameStates;

namespace Content.Shared.ADT.Infection.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MeleeInfectComponent : Component
{
    /// <summary>
    /// Шанс инфекции при ударе (0-1)
    /// </summary>
    [DataField, AutoNetworkedField]
    public float InfectionChance = 0.5f;
}