using Content.Shared.ADT.Amputation.Components;
using Content.Shared.Standing;
using Robust.Shared.GameStates;

namespace Content.Shared.ADT.Amputation;

public sealed class CannotStandSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CannotStandComponent, StandAttemptEvent>(OnStandAttempt);
    }

    private void OnStandAttempt(EntityUid uid, CannotStandComponent component, StandAttemptEvent args)
    {
        // Блокируем попытку встать
        args.Cancel();
    }
}