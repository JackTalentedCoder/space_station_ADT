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
        SubscribeLocalEvent<NecromancerComponent, NecromancerOrderActionEvent>(OnOrderAction);
        SubscribeLocalEvent<NecromancerComponent, NecromancerRaiseMinionActionEvent>(OnRaiseMinion);
        SubscribeLocalEvent<NecromancerComponent, NecromancerRaiseDeadActionEvent>(OnRaiseDead);
        SubscribeLocalEvent<NecromancerComponent, AfterPointedAtEvent>(OnPointedAt); // ЗДЕСЬ ОСТАЕТСЯ

        SubscribeLocalEvent<NecromancerMinionComponent, ComponentShutdown>(OnMinionShutdown);

        SubscribeLocalEvent<NecromancyAvailableComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnStartup(EntityUid uid, NecromancerComponent component, ComponentStartup args)
    {
        if (!TryComp(uid, out ActionsComponent? comp))
            return;

        _action.AddAction(uid, ref component.ActionRaiseMinionEntity, component.ActionRaiseMinion, component: comp);
        _action.AddAction(uid, ref component.ActionRaiseDeadEntity, component.ActionRaiseDead, component: comp);
        _action.AddAction(uid, ref component.ActionOrderStayEntity, component.ActionOrderStay, component: comp);
        _action.AddAction(uid, ref component.ActionOrderFollowEntity, component.ActionOrderFollow, component: comp);
        _action.AddAction(uid, ref component.ActionOrderAttackEntity, component.ActionOrderAttack, component: comp);
        _action.AddAction(uid, ref component.ActionOrderLooseEntity, component.ActionOrderLoose, component: comp);

        UpdateActions(uid, component);
    }

    private void OnShutdown(EntityUid uid, NecromancerComponent component, ComponentShutdown args)
    {
        // Освобождаем всех миньонов при смерти некроманта
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
        _action.RemoveAction(actions, component.ActionOrderLooseEntity);
    }

    private void OnOrderAction(EntityUid uid, NecromancerComponent component, NecromancerOrderActionEvent args)
    {
        if (component.CurrentOrder == args.Type)
            return;
        args.Handled = true;

        component.CurrentOrder = args.Type;
        Dirty(uid, component);

        DoCommandCallout(uid, component);
        UpdateActions(uid, component);
        UpdateAllMinions(uid, component);
    }

    private void OnRaiseMinion(EntityUid uid, NecromancerComponent component, NecromancerRaiseMinionActionEvent args)
    {
        if (args.Handled)
            return;

        // Это действие требует выбора цели, поэтому мы просто покажем сообщение
        _popup.PopupEntity(Loc.GetString("necromancer-raise-minion-instruction"), uid, uid);
        args.Handled = true;
    }

    private void OnRaiseDead(EntityUid uid, NecromancerComponent component, NecromancerRaiseDeadActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        MassRaiseDead(uid, component);
    }

    private void OnPointedAt(EntityUid uid, NecromancerComponent component, ref AfterPointedAtEvent args)
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
        if (TryComp(component.Necromancer, out NecromancerComponent? necromancerComponent))
            necromancerComponent.Minions.Remove(uid);
    }

    private void OnAfterInteract(EntityUid uid, NecromancyAvailableComponent component, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        // Проверяем, что взаимодействующий - некромант
        if (!HasComp<NecromancerComponent>(args.User))
            return;

        // Проверяем, что цель мертва и еще не воскрешена
        if (!TryComp<MobStateComponent>(args.Target, out var mobState) || !_mobState.IsDead(args.Target.Value, mobState))
            return;

        if (component.Raised)
        {
            _popup.PopupEntity(Loc.GetString("necromancer-already-raised"), args.Target.Value, args.User);
            return;
        }

        args.Handled = true;

        // Создаем DoAfter для процесса воскрешения
        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, component.RaiseDuration,
            new NecromancerRaiseMinionDoAfterEvent(), uid, target: args.Target, used: uid)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            DistanceThreshold = 2f,
            NeedHand = true
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void UpdateActions(EntityUid uid, NecromancerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        _action.SetToggled(component.ActionOrderStayEntity, component.CurrentOrder == NecromancerOrderType.Stay);
        _action.SetToggled(component.ActionOrderFollowEntity, component.CurrentOrder == NecromancerOrderType.Follow);
        _action.SetToggled(component.ActionOrderAttackEntity, component.CurrentOrder == NecromancerOrderType.Attack);
        _action.SetToggled(component.ActionOrderLooseEntity, component.CurrentOrder == NecromancerOrderType.Loose);
        _action.StartUseDelay(component.ActionOrderStayEntity);
        _action.StartUseDelay(component.ActionOrderFollowEntity);
        _action.StartUseDelay(component.ActionOrderAttackEntity);
        _action.StartUseDelay(component.ActionOrderLooseEntity);
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

    protected void RaiseMinion(EntityUid necromancer, EntityUid target, NecromancerComponent component, NecromancyAvailableComponent available)
    {
        // Помечаем как воскрешенный
        available.Raised = true;
        Dirty(target, available);

        // Добавляем компонент миньона
        var minionComp = EnsureComp<NecromancerMinionComponent>(target);
        minionComp.Necromancer = necromancer;
        Dirty(target, minionComp);

        // Добавляем в список миньонов некроманта
        component.Minions.Add(target);
        Dirty(necromancer, component);

        // Обновляем NPC миньона
        UpdateMinionNpc(target, component.CurrentOrder);

        // Воскрешаем существо
        if (TryComp<MobStateComponent>(target, out var mobState))
        {
            _mobState.ChangeMobState(target, MobState.Alive, mobState);
        }

        // Если указан прототип для превращения
        if (!string.IsNullOrEmpty(available.MinionPrototype))
        {
            TransformEntity(target, available.MinionPrototype);
        }

        // Проигрываем звук воскрешения
        if (available.RaiseSound != null)
        {
            _audio.PlayPredicted(available.RaiseSound, target, necromancer);
        }
    }

    protected virtual void TransformEntity(EntityUid entity, string prototype)
    {
        // Реализация превращения сущности в другой прототип
        // Это нужно делать на сервере
    }
}

[Serializable, NetSerializable]
public sealed partial class NecromancerRaiseMinionDoAfterEvent : SimpleDoAfterEvent
{
}