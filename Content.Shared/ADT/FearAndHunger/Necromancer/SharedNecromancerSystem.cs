using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.ADT.Necromancer;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Content.Shared.Necromancer;
using Content.Shared.Mobs;
using Content.Shared.Pointing;
using System.Linq;
using Robust.Shared.Utility;

namespace Content.Shared.ADT.Necromancer;

public abstract class SharedNecromancerSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] protected readonly IPrototypeManager PrototypeManager = default!;
    [Dependency] protected readonly IRobustRandom Random = default!;
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] protected readonly SharedAudioSystem _audio = default!;
    [Dependency] protected readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] protected readonly SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NecromancerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<NecromancerComponent, ComponentShutdown>(OnShutdown);

        // Оставляем подписку в Shared системе, но теперь метод protected
        SubscribeLocalEvent<NecromancerComponent, AfterPointedAtEvent>(OnPointedAt);

        SubscribeLocalEvent<NecromancerMinionComponent, ComponentShutdown>(OnMinionShutdown);
        SubscribeLocalEvent<NecromancyAvailableComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnStartup(EntityUid uid, NecromancerComponent component, ComponentStartup args)
    {
        if (!TryComp(uid, out ActionsComponent? comp))
            return;

        _action.AddAction(uid, ref component.ActionRaiseMinionEntity, component.ActionRaiseMinion, component: comp);

        if (component.SupremeNecromancy)
        {
            _action.AddAction(uid, ref component.ActionRaiseDeadEntity, component.ActionRaiseDead, component: comp);
        }

        _action.AddAction(uid, ref component.ActionOrderStayEntity, component.ActionOrderStay, component: comp);
        _action.AddAction(uid, ref component.ActionOrderFollowEntity, component.ActionOrderFollow, component: comp);
        _action.AddAction(uid, ref component.ActionOrderAttackEntity, component.ActionOrderAttack, component: comp);

        UpdateActions(uid, component);
    }

    private void OnShutdown(EntityUid uid, NecromancerComponent component, ComponentShutdown args)
    {
        foreach (var minion in component.Minions)
        {
            if (TryComp(minion, out NecromancerMinionComponent? minionComp))
                minionComp.Necromancer = null;
        }

        if (!TryComp(uid, out ActionsComponent? comp))
            return;

        var actions = new Entity<ActionsComponent?>(uid, comp);
        _action.RemoveAction(actions, component.ActionRaiseMinionEntity);
        _action.RemoveAction(actions, component.ActionRaiseDeadEntity);
        _action.RemoveAction(actions, component.ActionOrderStayEntity);
        _action.RemoveAction(actions, component.ActionOrderFollowEntity);
        _action.RemoveAction(actions, component.ActionOrderAttackEntity);
    }

    // ИЗМЕНЕНИЕ: меняем с private на protected virtual
    protected virtual void OnPointedAt(EntityUid uid, NecromancerComponent component, ref AfterPointedAtEvent args)
    {
        if (component.CurrentOrder != NecromancerOrderType.Attack)
            return;

        AfterPointedAt(uid, component, args);
    }

    protected virtual void AfterPointedAt(EntityUid uid, NecromancerComponent component, AfterPointedAtEvent args)
    {
        // Реализация на сервере
    }

    private void OnMinionShutdown(EntityUid uid, NecromancerMinionComponent component, ComponentShutdown args)
    {
        if (component.Necromancer != null &&
            TryComp(component.Necromancer.Value, out NecromancerComponent? necromancerComponent))
        {
            necromancerComponent.Minions.Remove(uid);
            Dirty(component.Necromancer.Value, necromancerComponent);
        }
    }

    private void OnAfterInteract(EntityUid uid, NecromancyAvailableComponent component, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        var targetUid = args.Target.Value;

        if (!HasComp<NecromancerComponent>(args.User))
            return;

        if (component.Raised)
        {
            _popup.PopupEntity(Loc.GetString("necromancer-already-raised"), targetUid, args.User, PopupType.Medium);
            return;
        }

        args.Handled = true;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(component.RaiseDuration),
            new NecromancerRaiseMinionDoAfterEvent(), uid, target: targetUid, used: args.User)
        {
            BreakOnDamage = false,
            BreakOnHandChange = true,
            BreakOnMove = true,
            DistanceThreshold = 2f,
            NeedHand = true,
            DuplicateCondition = DuplicateConditions.SameTool,
            Broadcast = true
        };

        _popup.PopupEntity("Начинаю воскрешение...", targetUid, args.User);
        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    // ИЗМЕНЕНИЕ: меняем с private на protected
    protected void UpdateActions(EntityUid uid, NecromancerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        _action.SetToggled(component.ActionOrderStayEntity, component.CurrentOrder == NecromancerOrderType.Stay);
        _action.SetToggled(component.ActionOrderFollowEntity, component.CurrentOrder == NecromancerOrderType.Follow);
        _action.SetToggled(component.ActionOrderAttackEntity, component.CurrentOrder == NecromancerOrderType.Attack);
        _action.StartUseDelay(component.ActionOrderStayEntity);
        _action.StartUseDelay(component.ActionOrderFollowEntity);
        _action.StartUseDelay(component.ActionOrderAttackEntity);
    }

    public void UpdateAllMinions(EntityUid uid, NecromancerComponent component)
    {
        foreach (var minion in component.Minions)
        {
            UpdateMinionNpc(minion, component.CurrentOrder);
        }
    }

    public virtual void UpdateMinionNpc(EntityUid uid, NecromancerOrderType orderType)
    {
        // Реализация на сервере
    }

    public virtual void DoCommandCallout(EntityUid uid, NecromancerComponent component)
    {
        // Реализация на сервере
    }

    public virtual void MassRaiseDead(EntityUid uid, NecromancerComponent component)
    {
        // Реализация на сервере
    }

    public void SetSupremeNecromancy(EntityUid uid, bool enabled, NecromancerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.SupremeNecromancy == enabled)
            return;

        component.SupremeNecromancy = enabled;
        Dirty(uid, component);

        if (TryComp<ActionsComponent>(uid, out var actionsComp))
        {
            if (enabled)
            {
                _action.AddAction(uid, ref component.ActionRaiseDeadEntity, component.ActionRaiseDead, component: actionsComp);
            }
            else
            {
                var actions = new Entity<ActionsComponent?>(uid, actionsComp);
                _action.RemoveAction(actions, component.ActionRaiseDeadEntity);
            }
        }

        var message = enabled
            ? "Вы обрели знание Верховной Некромантии!"
            : "Вы утратили знание Верховной Некромантии.";

        _popup.PopupEntity(message, uid, uid, PopupType.Medium);
    }
}

[Serializable, NetSerializable]
public sealed partial class NecromancerRaiseMinionDoAfterEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public sealed partial class NecromancerRaiseDeadDoAfterEvent : SimpleDoAfterEvent
{
}