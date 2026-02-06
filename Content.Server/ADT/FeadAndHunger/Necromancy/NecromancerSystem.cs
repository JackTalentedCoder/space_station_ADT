using Content.Server.Chat.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Server.Popups;
using Content.Shared.Actions;
using Content.Shared.ADT.Necromancer;
using Content.Shared.Dataset;
using Content.Shared.Mobs.Components;
using Content.Shared.Necromancer;
using Robust.Shared.Prototypes;
using Content.Shared.Mobs;
using System.Linq;
using Robust.Shared.Spawners;
using System.Numerics;
using Content.Shared.Pointing;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Content.Shared.Hands.Components;

namespace Content.Server.ADT.Necromancer;

public sealed class NecromancerSystem : SharedNecromancerSystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly HTNSystem _htn = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NecromancerRaiseMinionDoAfterEvent>(OnRaiseMinionDoAfter);
        SubscribeLocalEvent<NecromancerRaiseDeadDoAfterEvent>(OnRaiseDeadDoAfter);
    }

    private void OnRaiseMinionDoAfter(NecromancerRaiseMinionDoAfterEvent ev)
    {
        if (ev.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString("necromancer-raise-cancelled"), ev.User, ev.User);
            return;
        }

        if (ev.Target == null)
            return;

        var target = ev.Target.Value;
        var necromancer = ev.User;

        if (!TryComp<NecromancerComponent>(necromancer, out var necromancerComp) ||
            !TryComp<NecromancyAvailableComponent>(target, out var availableComp))
            return;

        if (!TryComp<MobStateComponent>(target, out var mobState) ||
            !_mobState.IsDead(target, mobState) ||
            availableComp.Raised)
            return;

        RaiseMinion(necromancer, target, necromancerComp, availableComp);
        _popup.PopupEntity(Loc.GetString("necromancer-raise-success"), target, necromancer);
    }

    private void OnRaiseDeadDoAfter(NecromancerRaiseDeadDoAfterEvent ev)
    {
        if (ev.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString("necromancer-mass-raise-cancelled"), ev.User, ev.User);
            return;
        }

        var necromancer = ev.User;

        if (!TryComp<NecromancerComponent>(necromancer, out var necromancerComp))
            return;

        MassRaiseDead(necromancer, necromancerComp);
    }

    protected override void AfterPointedAt(EntityUid uid, NecromancerComponent component, AfterPointedAtEvent args)
    {
        foreach (var minion in component.Minions)
        {
            _npc.SetBlackboard(minion, NPCBlackboard.CurrentOrderedTarget, args.Pointed);
        }
    }

    public override void UpdateMinionNpc(EntityUid uid, NecromancerOrderType orderType)
    {
        // Устанавливаем текущий приказ в черную доску
        _npc.SetBlackboard(uid, NPCBlackboard.CurrentOrders, orderType);

        // Обрабатываем специальные цели для разных приказов
        if (orderType == NecromancerOrderType.Follow)
        {
            // Для приказа "Следовать" устанавливаем цель следования
            if (TryComp<NecromancerMinionComponent>(uid, out var minionComp) && minionComp.Necromancer != null)
            {
                _npc.SetBlackboard(uid, NPCBlackboard.FollowTarget,
                    new EntityCoordinates(minionComp.Necromancer.Value, Vector2.Zero));
            }
            // Очищаем цель атаки при смене на режим следования
            _npc.SetBlackboard(uid, NPCBlackboard.CurrentOrderedTarget, null!);
        }
        else if (orderType == NecromancerOrderType.Attack)
        {
            // Для приказа "Атака" очищаем цель следования
            _npc.SetBlackboard(uid, NPCBlackboard.FollowTarget, EntityCoordinates.Invalid);
            // Цель атаки будет установлена через систему pointing
        }
        else // Stay
        {
            // Для приказа "Остаться" очищаем все цели
            _npc.SetBlackboard(uid, NPCBlackboard.FollowTarget, EntityCoordinates.Invalid);
            _npc.SetBlackboard(uid, NPCBlackboard.CurrentOrderedTarget, null!);
        }

        // Принудительно репланируем HTN для применения нового приказа
        if (TryComp<HTNComponent>(uid, out var htn))
        {
            if (htn.Plan != null)
                _htn.ShutdownPlan(htn);

            _htn.Replan(htn);
        }
    }

    public override void DoCommandCallout(EntityUid uid, NecromancerComponent component)
    {
        if (!component.OrderCallouts.TryGetValue(component.CurrentOrder, out var datasetId) ||
            !PrototypeManager.TryIndex<LocalizedDatasetPrototype>(datasetId, out var datasetPrototype))
            return;

        var values = datasetPrototype.Values.ToList();
        if (values.Count == 0)
            return;

        var msg = values[Random.Next(values.Count)];
        _chat.TrySendInGameICMessage(uid, msg, InGameICChatType.Speak, true);
    }

    public override void MassRaiseDead(EntityUid uid, NecromancerComponent component)
    {
        var xform = Transform(uid);
        var raisedCount = 0;

        var query = EntityQueryEnumerator<NecromancyAvailableComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var entity, out var available, out var mobState, out var targetXform))
        {
            if (available.Raised)
                continue;

            var distance = (targetXform.WorldPosition - xform.WorldPosition).Length();
            if (distance > component.RaiseDeadRadius)
                continue;

            if (!_mobState.IsDead(entity, mobState))
                continue;

            RaiseMinion(uid, entity, component, available);
            raisedCount++;
        }

        if (raisedCount > 0)
        {
            _popup.PopupEntity(Loc.GetString("necromancer-mass-raise-success", ("count", raisedCount)), uid, uid);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("necromancer-no-available-corpses"), uid, uid);
        }
    }

    protected override void TransformEntity(EntityUid entity, string prototype, NecromancyAvailableComponent available)
    {
        if (string.IsNullOrEmpty(prototype))
            return;

        var transform = Transform(entity);
        var coordinates = _transform.GetMoverCoordinates(entity);
        var rotation = transform.LocalRotation;

        // Сохраняем данные о некроманте
        EntityUid? necromancer = null;
        if (TryComp<NecromancerMinionComponent>(entity, out var minionComp))
        {
            necromancer = minionComp.Necromancer;
        }

        // Сохраняем компонент Hands, если нужно
        HandsComponent? hands = null;
        if (available.KeepHands && TryComp<HandsComponent>(entity, out var handsComp))
        {
            // Копируем компонент
            hands = new HandsComponent
            {
                ActiveHandId = handsComp.ActiveHandId,
                Hands = new Dictionary<string, Hand>(handsComp.Hands),
                SortedHands = new List<string>(handsComp.SortedHands),
                DisableExplosionRecursion = handsComp.DisableExplosionRecursion,
                BaseThrowspeed = handsComp.BaseThrowspeed,
                ThrowRange = handsComp.ThrowRange,
                ShowInHands = handsComp.ShowInHands,
                NextThrowTime = handsComp.NextThrowTime,
                ThrowCooldown = handsComp.ThrowCooldown,
                HandDisplacement = handsComp.HandDisplacement,
                LeftHandDisplacement = handsComp.LeftHandDisplacement,
                RightHandDisplacement = handsComp.RightHandDisplacement,
                InHandItemScale = handsComp.InHandItemScale,
                CanBeStripped = handsComp.CanBeStripped
            };
        }

        // Удаляем старую сущность
        Del(entity);

        // Создаем новую сущность по прототипу
        var newEntity = _entityManager.SpawnEntity(prototype, coordinates);

        // Восстанавливаем вращение
        var newTransform = Transform(newEntity);
        _transform.SetLocalRotation(newEntity, rotation, newTransform);

        // Восстанавливаем компонент Hands, если нужно
        if (hands != null)
        {
            var newHands = EnsureComp<HandsComponent>(newEntity);

            // Копируем все свойства
            newHands.ActiveHandId = hands.ActiveHandId;
            newHands.Hands = new Dictionary<string, Hand>(hands.Hands);
            newHands.SortedHands = new List<string>(hands.SortedHands);
            newHands.DisableExplosionRecursion = hands.DisableExplosionRecursion;
            newHands.BaseThrowspeed = hands.BaseThrowspeed;
            newHands.ThrowRange = hands.ThrowRange;
            newHands.ShowInHands = hands.ShowInHands;
            newHands.NextThrowTime = hands.NextThrowTime;
            newHands.ThrowCooldown = hands.ThrowCooldown;
            newHands.HandDisplacement = hands.HandDisplacement;
            newHands.LeftHandDisplacement = hands.LeftHandDisplacement;
            newHands.RightHandDisplacement = hands.RightHandDisplacement;
            newHands.InHandItemScale = hands.InHandItemScale;
            newHands.CanBeStripped = hands.CanBeStripped;

            Dirty(newEntity, newHands);
        }
        else if (available.KeepHands == false)
        {
            // Удаляем компонент Hands, если он есть
            RemComp<HandsComponent>(newEntity);
        }

        // Восстанавливаем связь с некромантом
        if (necromancer != null && TryComp<NecromancerComponent>(necromancer, out var necromancerComp))
        {
            // Добавляем компонент миньона к новой сущности
            var newMinionComp = EnsureComp<NecromancerMinionComponent>(newEntity);
            newMinionComp.Necromancer = necromancer;
            Dirty(newEntity, newMinionComp);

            // Обновляем список миньонов у некроманта
            necromancerComp.Minions.Remove(entity);
            necromancerComp.Minions.Add(newEntity);
            Dirty(necromancer.Value, necromancerComp);

            // Применяем текущий приказ к новому миньону
            UpdateMinionNpc(newEntity, necromancerComp.CurrentOrder);
        }
    }
}