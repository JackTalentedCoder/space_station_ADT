using Content.Shared.ADT.Infection.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;

namespace Content.Shared.ADT.Infection;

public sealed class ContactInfectionSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ContactInfectionComponent, StartCollideEvent>(OnEnter);
        SubscribeLocalEvent<ContactInfectionComponent, EndCollideEvent>(OnExit);
    }

    private void OnEnter(EntityUid uid, ContactInfectionComponent comp, ref StartCollideEvent args)
    {
        var other = args.OtherEntity;

        // Проверяем, есть ли уже инфекция рук или ног
        if (HasComp<HandInfectionComponent>(other) || HasComp<LegInfectionComponent>(other))
            return;

        if (comp.InfectionType == InfectionType.None)
            return;

        if (comp.IgnoreWhitelist != null && _whitelist.IsWhitelistPass(comp.IgnoreWhitelist, other))
            return;

        // Случайный выбор типа инфекции, если установлен Both
        var infectionType = comp.InfectionType;
        if (infectionType == InfectionType.Both)
        {
            infectionType = _random.Prob(0.5f) ? InfectionType.Hands : InfectionType.Legs;
        }

        // Создаем соответствующую инфекцию
        switch (infectionType)
        {
            case InfectionType.Hands:
                var handInf = EnsureComp<HandInfectionComponent>(other);
                Dirty(other, handInf);
                break;

            case InfectionType.Legs:
                var legInf = EnsureComp<LegInfectionComponent>(other);
                Dirty(other, legInf);
                break;
        }
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

public enum InfectionType
{
    None = 0,
    Hands = 1,
    Legs = 2,
    Both = 3
}