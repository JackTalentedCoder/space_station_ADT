using Content.Shared.Alert;

namespace Content.Shared.ADT.Sanity;

/// <summary>
/// Событие, вызываемое при нажатии на алерт HumanSanity.
/// </summary>
public sealed partial class ShowSanityEvent : BaseAlertEvent
{
    // Сущность берётся из AttachedEntity сессии игрока, отдельное поле не требуется.
}