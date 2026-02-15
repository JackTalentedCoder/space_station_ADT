using Content.Server.Chat.Managers;
using Content.Shared.ADT.Sanity;
using Content.Shared.Alert;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Rejuvenate;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.ADT.Sanity;

public sealed class SanitySystem : SharedSanitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private const float DamageMultiplier = 1.5f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SanityComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SanityComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SanityComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<SanityComponent, ShowSanityEvent>(OnShowSanity);
        SubscribeLocalEvent<PanophobiaComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnMapInit(EntityUid uid, SanityComponent component, MapInitEvent args)
    {
        component.CurrentSanity = component.MaxSanity;
        component.NextDecayTime = _timing.CurTime + component.DecayInterval;
        Dirty(uid, component);
        UpdateAlert(uid, component);
        UpdatePanophobia(uid, component);
    }

    private void OnShutdown(EntityUid uid, SanityComponent component, ComponentShutdown args)
    {
        _alerts.ClearAlert(uid, SanityAlertId);
    }

    private void OnRejuvenate(EntityUid uid, SanityComponent component, RejuvenateEvent args)
    {
        SetSanity(uid, component.MaxSanity, component);
    }

    private void OnShowSanity(EntityUid uid, SanityComponent component, ShowSanityEvent args)
    {
        if (!_player.TryGetSessionByEntity(uid, out var session))
            return;

        var sanityValue = (int)Math.Round(component.CurrentSanity);
        var message = Loc.GetString("sanity-current", ("sanity", sanityValue));

        _chat.ChatMessageToOne(
            ChatChannel.Local,
            message,
            message,
            uid,
            false,
            session.Channel);
    }

    private void OnDamageModify(EntityUid uid, PanophobiaComponent component, DamageModifyEvent args)
    {
        foreach (var (type, value) in args.Damage.DamageDict)
        {
            if (value > 0)
                args.Damage.DamageDict[type] = value * DamageMultiplier;
        }
    }

    public override void ModifySanity(EntityUid uid, float delta, SanityComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;
        SetSanity(uid, component.CurrentSanity + delta, component);
    }

    public override void SetSanity(EntityUid uid, float value, SanityComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        value = Math.Clamp(value, 0, component.MaxSanity);
        if (MathHelper.CloseTo(component.CurrentSanity, value))
            return;

        component.CurrentSanity = value;
        Dirty(uid, component);
        UpdateAlert(uid, component);
        UpdatePanophobia(uid, component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SanityComponent>();
        var curTime = _timing.CurTime;

        while (query.MoveNext(out var uid, out var sanity))
        {
            while (curTime >= sanity.NextDecayTime)
            {
                ModifySanity(uid, -1, sanity);
                sanity.NextDecayTime += sanity.DecayInterval;
                Dirty(uid, sanity);
            }
        }
    }
}