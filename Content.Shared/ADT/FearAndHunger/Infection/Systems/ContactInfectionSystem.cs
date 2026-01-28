using Content.Shared.ADT.Infection.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.ADT.Infection;

public sealed class ContactInfectionSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ContactInfectionComponent, StartCollideEvent>(OnEnter);
        SubscribeLocalEvent<ContactInfectionComponent, EndCollideEvent>(OnExit);
    }

    private void OnEnter(EntityUid uid, ContactInfectionComponent comp, ref StartCollideEvent args)
    {
        var other = args.OtherEntity;

        if (HasComp<PendingInfectionComponent>(other))
            return;

        if (comp.IgnoreWhitelist != null && _whitelist.IsWhitelistPass(comp.IgnoreWhitelist, other))
            return;

        var inf = EnsureComp<PendingInfectionComponent>(other);
        Dirty(other, inf);
    }

    private void OnExit(EntityUid uid, ContactInfectionComponent comp, ref EndCollideEvent args)
    {
        var other = args.OtherEntity;

        if (!TryComp<PhysicsComponent>(other, out var body))
            return;

        var hasContact = false;
        var query = GetEntityQuery<ContactInfectionComponent>();

        foreach (var contact in _physics.GetContactingEntities(other, body))
        {
            if (contact == uid)
                continue;

            if (query.HasComponent(contact))
            {
                hasContact = true;
                break;
            }
        }

        if (!hasContact)
        {
            // Можно RemComp если нужно, но обычно оставляем (инфекция уже есть)
        }
    }
}