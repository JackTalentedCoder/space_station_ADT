using Robust.Shared.GameStates;

namespace Content.Shared.ADT.Sanity;

/// <summary>
/// Маркерный компонент: если есть на сущности, входящий урон увеличивается на 50%.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PanophobiaComponent : Component;