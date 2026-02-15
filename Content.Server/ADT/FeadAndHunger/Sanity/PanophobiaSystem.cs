using Content.Shared.ADT.Sanity.Components;
using Content.Shared.ADT.Sanity;
using Content.Shared.Damage;
using Robust.Shared.Player;
using Content.Server.Chat.Managers;
using Content.Shared.Chat;

namespace Content.Server.ADT.Sanity;

public sealed class ServerSanitySystem : SharedSanitySystem
{
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShowSanityEvent>(OnShowSanity);
        SubscribeLocalEvent<SanityComponent, ComponentStartup>(OnSanityStartup);
        SubscribeLocalEvent<SanityComponent, ComponentShutdown>(OnSanityShutdown);
    }

    private new void OnSanityStartup(EntityUid uid, SanityComponent component, ComponentStartup args)
    {
        base.OnSanityStartup(uid, component, args);
        UpdatePanophobia(uid, component);
    }

    private new void OnSanityShutdown(EntityUid uid, SanityComponent component, ComponentShutdown args)
    {
        base.OnSanityShutdown(uid, component, args);
        RemComp<PanophobiaComponent>(uid);
    }

    private void OnShowSanity(ShowSanityEvent args)
    {
        if (args.Handled)
            return;

        var uid = args.Entity;
        if (!TryComp<SanityComponent>(uid, out var sanity))
            return;

        if (!_playerManager.TryGetSessionByEntity(uid, out var session))
            return;

        var message = Loc.GetString("sanity-current", ("sanity", (int)sanity.CurrentSanity));

        _chat.ChatMessageToOne(
            ChatChannel.Emotes,
            message,
            message,
            uid,
            false,
            session.Channel,
            Color.Yellow
        );

        args.Handled = true;
    }

    public void SetSanity(EntityUid uid, float amount, SanityComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var oldSanity = component.CurrentSanity;
        component.CurrentSanity = Math.Clamp(amount, 0f, 100f);
        Dirty(uid, component);

        UpdateSanityAlert(uid, component);

        if ((oldSanity >= component.PanicThreshold && component.CurrentSanity < component.PanicThreshold) ||
            (oldSanity < component.PanicThreshold && component.CurrentSanity >= component.PanicThreshold))
        {
            UpdatePanophobia(uid, component);
        }
    }

    public void ModifySanity(EntityUid uid, float amount, SanityComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        SetSanity(uid, component.CurrentSanity + amount, component);
    }

    private void UpdatePanophobia(EntityUid uid, SanityComponent component)
    {
        if (component.CurrentSanity < component.PanicThreshold)
        {
            var panophobia = EnsureComp<PanophobiaComponent>(uid);
            panophobia.DamageMultiplier = 1.5f;
            Dirty(uid, panophobia);
        }
        else
        {
            RemComp<PanophobiaComponent>(uid);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<SanityComponent>();

        while (query.MoveNext(out var uid, out var sanity))
        {
            if (curTime < sanity.NextDecayTime)
                continue;

            ModifySanity(uid, -sanity.DecayRate, sanity);
            sanity.NextDecayTime = curTime + sanity.DecayInterval;
            Dirty(uid, sanity);
        }
    }
}