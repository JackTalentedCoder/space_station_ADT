using Content.Shared.ADT.Sanity;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared.ADT.Sanity;

public sealed partial class AdjustSanity : EntityEffect
{
    private const float DefaultSanityFactor = 10f;

    [DataField("factor")]
    public float SanityFactor { get; set; } = DefaultSanityFactor;

    public override void Effect(EntityEffectBaseArgs args)
    {
        var uid = args.TargetEntity;
        if (args.EntityManager.TryGetComponent<SanityComponent>(uid, out var sanity))
        {
            // Используем общий интерфейс SharedSanitySystem
            var sanitySystem = args.EntityManager.System<SharedSanitySystem>();
            sanitySystem.ModifySanity(uid, SanityFactor, sanity);
        }
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return Loc.GetString("reagent-effect-guidebook-adjust-sanity",
            ("chance", Probability),
            ("relative", SanityFactor / DefaultSanityFactor));
    }
}