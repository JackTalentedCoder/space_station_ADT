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
            if (_random.Prob(comp.InfectionChance))
            {
                var inf = EnsureComp<PendingInfectionComponent>(hit);
                // Можно настроить grace/damage здесь, если нужно через DataField
                Dirty(hit, inf);
            }
        }
    }
}