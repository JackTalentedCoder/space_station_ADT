using System.Linq;
using Content.Shared.ADT.Infection.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Random;

namespace Content.Shared.ADT.Infection;

public sealed class MeleeInfectSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MeleeInfectComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(EntityUid uid, MeleeInfectComponent comp, MeleeHitEvent args)
    {
        if (!args.HitEntities.Any())
            return;

        foreach (var hit in args.HitEntities)
        {
            // Проверяем, есть ли уже инфекция
            if ((comp.InfectionType == InfectionType.Hands || comp.InfectionType == InfectionType.Both) &&
                HasComp<HandInfectionComponent>(hit))
                continue;

            if ((comp.InfectionType == InfectionType.Legs || comp.InfectionType == InfectionType.Both) &&
                HasComp<LegInfectionComponent>(hit))
                continue;

            if (_random.Prob(comp.InfectionChance))
            {
                // Случайный выбор типа инфекции, если установлен Both
                var infectionType = comp.InfectionType;
                if (infectionType == InfectionType.Both)
                {
                    infectionType = _random.Prob(0.5f) ? InfectionType.Hands : InfectionType.Legs;
                }

                switch (infectionType)
                {
                    case InfectionType.Hands:
                        var handInf = EnsureComp<HandInfectionComponent>(hit);
                        Dirty(hit, handInf);
                        break;

                    case InfectionType.Legs:
                        var legInf = EnsureComp<LegInfectionComponent>(hit);
                        Dirty(hit, legInf);
                        break;
                }
            }
        }
    }
}