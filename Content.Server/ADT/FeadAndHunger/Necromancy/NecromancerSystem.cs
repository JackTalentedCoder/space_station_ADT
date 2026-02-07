using Content.Server.Chat.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Server.Popups;
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
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Utility;

namespace Content.Server.ADT.Necromancer;

public sealed class NecromancerSystem : SharedNecromancerSystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NecromancerRaiseMinionDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<NecromancerRaiseDeadDoAfterEvent>(OnDoAfterMass);
    }

    private void OnDoAfter(NecromancerRaiseMinionDoAfterEvent ev)
    {
        if (ev.Used != null && TryComp<NecromancerComponent>(ev.Used, out var necromancerComp))
        {
            if (necromancerComp.RaiseProcessSound != null)
            {
                _audio.PlayPvs(necromancerComp.RaiseProcessSound, ev.Used.Value);
            }
        }

        if (ev.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString("necromancer-raise-cancelled"), ev.Target ?? ev.User, ev.User);
            return;
        }

        if (!TryComp<NecromancerComponent>(ev.Used, out var tool) ||
            ev.Target is not {} target ||
            !TryComp<NecromancyAvailableComponent>(target, out var availableComp))
        {
            _popup.PopupEntity(Loc.GetString("necromancer-failed"), ev.Target ?? ev.User, ev.User);
            return;
        }

        if (availableComp.Raised)
        {
            _popup.PopupEntity(Loc.GetString("necromancer-already-raised"), target, ev.User);
            return;
        }

        PerformNecromancy(target, ev.Used.Value, tool, availableComp);
    }

    private void OnDoAfterMass(NecromancerRaiseDeadDoAfterEvent ev)
    {
        if (ev.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString("necromancer-mass-raise-cancelled"), ev.User, ev.User);
            return;
        }

        if (!TryComp<NecromancerComponent>(ev.Used, out var necromancerComp))
            return;

        PerformMassNecromancy(ev.Used.Value, necromancerComp);
    }

    private void PerformNecromancy(EntityUid target, EntityUid necromancer, NecromancerComponent component, NecromancyAvailableComponent available)
    {
        available.Raised = true;
        Dirty(target, available);

        TransformToMinion(target, available.MinionPrototype, necromancer, component);

        _popup.PopupEntity(Loc.GetString("necromancer-raise-success"), target, necromancer, PopupType.Medium);
    }

    private void PerformMassNecromancy(EntityUid necromancer, NecromancerComponent component)
    {
        var xform = Transform(necromancer);
        var raisedCount = 0;

        var query = EntityQueryEnumerator<NecromancyAvailableComponent, TransformComponent>();
        while (query.MoveNext(out var entity, out var available, out var targetXform))
        {
            if (available.Raised)
                continue;

            var distance = (targetXform.WorldPosition - xform.WorldPosition).Length();
            if (distance > component.RaiseDeadRadius)
                continue;

            available.Raised = true;
            Dirty(entity, available);

            TransformToMinion(entity, available.MinionPrototype, necromancer, component);
            raisedCount++;
        }

        if (raisedCount > 0)
        {
            _popup.PopupEntity(Loc.GetString("necromancer-mass-raise-success", ("count", raisedCount)), necromancer, necromancer);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("necromancer-no-available-corpses"), necromancer, necromancer);
        }
    }

    private void TransformToMinion(EntityUid entity, string prototype, EntityUid necromancer, NecromancerComponent component)
    {
        if (string.IsNullOrEmpty(prototype))
        {
            _popup.PopupEntity("Ошибка: не указан прототип для превращения", necromancer, necromancer, PopupType.Small);
            return;
        }

        try
        {
            var transform = Transform(entity);
            var coordinates = transform.Coordinates;
            var rotation = transform.LocalRotation;

            var newEntity = _entityManager.SpawnEntity(prototype, coordinates);

            _transform.SetWorldRotation(newEntity, rotation);

            var minionComp = EnsureComp<NecromancerMinionComponent>(newEntity);
            minionComp.Necromancer = necromancer;
            Dirty(newEntity, minionComp);

            component.Minions.Add(newEntity);
            Dirty(necromancer, component);

            // Инициализируем черную доску NPC перед применением приказа
            if (TryComp<HTNComponent>(newEntity, out var htn))
            {
                _npc.SetBlackboard(newEntity, NPCBlackboard.CurrentOrderedTarget, EntityUid.Invalid);
                _npc.SetBlackboard(newEntity, NPCBlackboard.FollowTarget, EntityCoordinates.Invalid);
                _npc.SetBlackboard(newEntity, NPCBlackboard.CurrentOrders, NecromancerOrderType.Follow);

                if (component.CurrentOrder == NecromancerOrderType.Follow)
                {
                    _npc.SetBlackboard(newEntity, NPCBlackboard.FollowTarget,
                        new EntityCoordinates(necromancer, Vector2.Zero));
                }
            }

            UpdateMinionNpc(newEntity, component.CurrentOrder);

            Del(entity);

            _popup.PopupEntity($"Создан новый миньон!", necromancer, necromancer, PopupType.Medium);
        }
        catch (Exception ex)
        {
            _popup.PopupEntity($"Ошибка при создании миньона: {ex.Message}", necromancer, necromancer, PopupType.Medium);
            Logger.Error($"Ошибка в TransformToMinion: {ex}");
        }
    }

    protected override void AfterPointedAt(EntityUid uid, NecromancerComponent component, AfterPointedAtEvent args)
    {
        if (args.Pointed == EntityUid.Invalid || !Exists(args.Pointed))
            return;

        foreach (var minion in component.Minions)
        {
            if (!Exists(minion) || !HasComp<HTNComponent>(minion))
                continue;

            _npc.SetBlackboard(minion, NPCBlackboard.CurrentOrderedTarget, args.Pointed);

            if (TryComp<HTNComponent>(minion, out var htn))
            {
                if (htn.Plan != null)
                    _htn.ShutdownPlan(htn);
                _htn.Replan(htn);
            }
        }
    }

    public override void UpdateMinionNpc(EntityUid uid, NecromancerOrderType orderType)
    {
        _npc.SetBlackboard(uid, NPCBlackboard.CurrentOrders, orderType);

        if (orderType == NecromancerOrderType.Follow)
        {
            if (TryComp<NecromancerMinionComponent>(uid, out var minionComp) && minionComp.Necromancer != null)
            {
                _npc.SetBlackboard(uid, NPCBlackboard.FollowTarget,
                    new EntityCoordinates(minionComp.Necromancer.Value, Vector2.Zero));
            }
            _npc.SetBlackboard(uid, NPCBlackboard.CurrentOrderedTarget, EntityUid.Invalid);
        }
        else if (orderType == NecromancerOrderType.Attack)
        {
            _npc.SetBlackboard(uid, NPCBlackboard.FollowTarget, EntityCoordinates.Invalid);
        }
        else // Stay
        {
            _npc.SetBlackboard(uid, NPCBlackboard.FollowTarget, EntityCoordinates.Invalid);
            _npc.SetBlackboard(uid, NPCBlackboard.CurrentOrderedTarget, EntityUid.Invalid);
        }

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

        var values = datasetPrototype.Values;
        if (values.Count == 0)
            return;

        var msg = values[Random.Next(values.Count)];
        _chat.TrySendInGameICMessage(uid, msg, InGameICChatType.Speak, true);
    }
}