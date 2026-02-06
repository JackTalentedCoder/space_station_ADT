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
        // УБРАТЬ ЭТУ СТРОКУ: SubscribeLocalEvent<NecromancerComponent, AfterPointedAtEvent>(OnPointedAt);
        // Подписка уже есть в базовом классе
    }

    private void OnRaiseMinionDoAfter(NecromancerRaiseMinionDoAfterEvent ev)
    {
        if (ev.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString("necromancer-raise-cancelled"), ev.User, ev.User);
            return;
        }

        if (ev.Target == null || ev.Used == null)
            return;

        var target = ev.Target.Value;
        var necromancer = ev.Used.Value;

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
        else if (orderType == NecromancerOrderType.Loose)
        {
            // Для приказа "Свободно" очищаем все цели
            _npc.SetBlackboard(uid, NPCBlackboard.FollowTarget, EntityCoordinates.Invalid);
            _npc.SetBlackboard(uid, NPCBlackboard.CurrentOrderedTarget, null!);
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

    protected override void TransformEntity(EntityUid entity, string prototype)
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

        // Удаляем старую сущность
        Del(entity);

        // Создаем новую сущность по прототипу
        var newEntity = _entityManager.SpawnEntity(prototype, coordinates);

        // Восстанавливаем вращение
        var newTransform = Transform(newEntity);
        _transform.SetLocalRotation(newEntity, rotation, newTransform);

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