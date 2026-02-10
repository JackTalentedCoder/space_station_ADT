using Content.Shared.Alert;
using Robust.Shared.Serialization;

namespace Content.Shared.ADT.Sanity;

[Serializable, NetSerializable]
public sealed partial class ShowSanityEvent : BaseAlertEvent
{
}