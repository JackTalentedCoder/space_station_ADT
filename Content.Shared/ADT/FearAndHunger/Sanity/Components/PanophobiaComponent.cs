using Robust.Shared.GameStates;

namespace Content.Shared.ADT.Sanity.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class PanophobiaComponent : Component
{
    /// <summary>
    /// Множитель получаемого урона
    /// </summary>
    [DataField("damageMultiplier"), ViewVariables(VVAccess.ReadWrite)]
    public float DamageMultiplier = 1.5f; // 50% увеличение урона
}