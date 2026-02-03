using Robust.Shared.GameStates;

namespace Content.Shared.ADT.Amputation.Components;

/// <summary>
/// Компонент, который блокирует возможность вставания персонажа
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CannotStandComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Permanent = true;
}