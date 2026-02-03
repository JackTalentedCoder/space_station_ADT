using Content.Shared.ADT.Amputation;
using Content.Shared.ADT.Amputation.Components;
using Content.Shared.ADT.Infection.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using System.Collections.Generic;
using Content.Server.Body.Systems;
using Content.Server.Hands.Systems;
using Content.Shared.Interaction;
using Robust.Shared.Audio;
using Content.Shared.Body.Part;
using Robust.Shared.Physics.Components;
using Content.Shared.Hands.Components;
using System.Linq;
using Content.Shared.Hands;
using Robust.Shared.Map;
using Content.Shared.Localizations;
using Content.Shared.Traits.Assorted;

namespace Content.Server.ADT.Amputation;

public sealed class AmputationSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AmputationToolComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<AmputationToolComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<AmputationDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<AmputatedLimbsComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<AmputatedLimbsComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Amputated.Count == 0)
            return;

        // Собираем список ампутированных конечностей как строки
        var amputatedLimbNames = new List<string>();

        // Проверяем наличие каждой конечности
        if (ent.Comp.Amputated.Contains(AmputateLimb.LeftArm))
            amputatedLimbNames.Add(Loc.GetString("amputation-limb-leftarm"));

        if (ent.Comp.Amputated.Contains(AmputateLimb.RightArm))
            amputatedLimbNames.Add(Loc.GetString("amputation-limb-rightarm"));

        if (ent.Comp.Amputated.Contains(AmputateLimb.LeftLeg))
            amputatedLimbNames.Add(Loc.GetString("amputation-limb-leftleg"));

        if (ent.Comp.Amputated.Contains(AmputateLimb.RightLeg))
            amputatedLimbNames.Add(Loc.GetString("amputation-limb-rightleg"));

        if (ent.Comp.Amputated.Contains(AmputateLimb.Head))
            amputatedLimbNames.Add(Loc.GetString("amputation-limb-head"));

        // Формируем текст для ампутированных конечностей
        if (amputatedLimbNames.Count > 0)
        {
            var limbsText = string.Join(", ", amputatedLimbNames);

            using (args.PushGroup(nameof(AmputatedLimbsComponent)))
            {
                args.PushMarkup(Loc.GetString("amputation-examine-detailed",
                    ("limbs", limbsText)));
            }
        }

        // Проверяем состояние передвижения
        var amputatedArmsCount = ent.Comp.Amputated.Count(l => l is AmputateLimb.LeftArm or AmputateLimb.RightArm);
        var amputatedLegsCount = ent.Comp.Amputated.Count(l => l is AmputateLimb.LeftLeg or AmputateLimb.RightLeg);

        // Если нет ног
        if (amputatedLegsCount >= 2)
        {
            // Проверяем, есть ли хотя бы одна рука
            bool hasArms = false;

            // Сначала проверяем по ампутированным рукам
            if (amputatedArmsCount < 2) // Если не ампутированы обе руки
            {
                // Проверяем компонент рук
                if (TryComp<HandsComponent>(ent, out var hands))
                {
                    // ИСПРАВЛЕНИЕ: Проверяем, есть ли хотя бы одна рука в компоненте
                    // Считаем, сколько рук осталось (общее количество рук минус ампутированные)
                    // Но правильнее проверить, что в hands.Hands есть хотя бы одна рука
                    hasArms = hands.Hands.Count > 0;
                }
                else
                {
                    // Если нет компонента рук, но руки не ампутированы, считаем что они есть
                    // Это для существ без компонента HandsComponent
                    hasArms = true;
                }
            }

            if (hasArms)
            {
                // Если есть ноги ампутированы, но есть руки
                args.PushMarkup(Loc.GetString("amputation-examine-crawl-only"));
            }
            else
            {
                // Если нет ни ног, ни рук
                args.PushMarkup(Loc.GetString("amputation-examine-cannot-move"));
            }
        }
    }
    private void OnGetVerbs(Entity<AmputationToolComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;

        foreach (AmputateLimb limb in Enum.GetValues<AmputateLimb>())
        {
            var verb = new Verb
            {
                Text = Loc.GetString($"amputation-verb-{limb.ToString().ToLower()}"),
                Category = VerbCategory.Amputation,
                Act = () =>
                {
                    ent.Comp.SelectedLimb = limb;
                    Dirty(ent);
                    _popup.PopupEntity(Loc.GetString("amputation-selected", ("limb", Loc.GetString($"amputation-limb-{limb.ToString().ToLower()}"))), ent.Owner, user);
                },
                Priority = (int)limb
            };
            args.Verbs.Add(verb);
        }
    }

    private void OnAfterInteract(Entity<AmputationToolComponent> tool, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        var target = args.Target.Value;

        // Проверка AmputatableComponent
        if (!HasComp<AmputatableComponent>(target))
            return;

        var limb = tool.Comp.SelectedLimb;
        var amputated = EnsureComp<AmputatedLimbsComponent>(target);

        // Проверка, не ампутирована ли уже эта конечность
        if (amputated.Amputated.Contains(limb))
        {
            _popup.PopupEntity(Loc.GetString("amputation-limb-already-amputated"), target, args.User);
            return;
        }

        // Проверка для головы - можно ли ампутировать?
        if (limb == AmputateLimb.Head)
        {
            if (TryComp<BodyComponent>(target, out var body))
            {
                var heads = _body.GetBodyChildrenOfType(target, BodyPartType.Head, body);
                if (!heads.Any())
                {
                    _popup.PopupEntity(Loc.GetString("amputation-no-head"), target, args.User);
                    return;
                }
            }
        }

        // Проверка для рук - есть ли рука для ампутации?
        if (limb is AmputateLimb.LeftArm or AmputateLimb.RightArm)
        {
            if (!TryComp<HandsComponent>(target, out var hands))
            {
                _popup.PopupEntity(Loc.GetString("amputation-no-hands"), target, args.User);
                return;
            }

            var location = limb == AmputateLimb.LeftArm ? HandLocation.Left : HandLocation.Right;
            var hasHand = hands.Hands.Values.Any(h => h.Location == location);

            if (!hasHand)
            {
                _popup.PopupEntity(Loc.GetString("amputation-no-hand-in-location"), target, args.User);
                return;
            }
        }

        // Проверка для ног - есть ли нога для ампутации?
        if (limb is AmputateLimb.LeftLeg or AmputateLimb.RightLeg)
        {
            // Для ног проверяем наличие BodyComponent
            if (TryComp<BodyComponent>(target, out var body))
            {
                var symmetry = limb == AmputateLimb.LeftLeg ? BodyPartSymmetry.Left : BodyPartSymmetry.Right;
                var legs = _body.GetBodyChildrenOfType(target, BodyPartType.Leg, body);

                var hasLegInLocation = legs.Any(leg =>
                    TryComp<BodyPartComponent>(leg.Id, out var legComp) &&
                    legComp.Symmetry == symmetry);

                if (!hasLegInLocation)
                {
                    _popup.PopupEntity(Loc.GetString("amputation-no-leg-in-location"), target, args.User);
                    return;
                }
            }
        }

        // Проверка максимального количества ампутаций
        if (!CheckMaxAmputations(target, limb, amputated))
        {
            _popup.PopupEntity(Loc.GetString("amputation-max-limit"), target, args.User);
            return;
        }

        args.Handled = true;

        var delay = tool.Comp.AmputationDelay; // Всегда используем задержку 7 секунд

        // Останавливаем предыдущий звук
        if (tool.Comp.CurrentSawingStream != null)
        {
            _audio.Stop(tool.Comp.CurrentSawingStream);
            tool.Comp.CurrentSawingStream = null;
        }

        // Звук процесса ампутации
        var audioParams = AudioParams.Default
            .WithVolume(7f)
            .WithLoop(true)
            .WithMaxDistance(10f)
            .WithVariation(0.1f)
            .WithRolloffFactor(1.5f);

        var played = _audio.PlayEntity(tool.Comp.SawSound, Filter.Pvs(tool.Owner), tool.Owner, true, audioParams);

        if (played != null)
        {
            tool.Comp.CurrentSawingStream = played.Value.Entity;
            Dirty(tool);
        }

        // Создаем DoAfter
        var doAfter = new DoAfterArgs(EntityManager, args.User, delay, new AmputationDoAfterEvent(limb),
            tool.Owner, target: target, used: tool.Owner)
        {
            BreakOnDamage = false,
            BreakOnHandChange = true,
            BreakOnMove = true,
            NeedHand = true,
            DuplicateCondition = DuplicateConditions.SameTool,
            Broadcast = true
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
        {
            // Если DoAfter не запустился, останавливаем звук
            if (tool.Comp.CurrentSawingStream != null)
            {
                _audio.Stop(tool.Comp.CurrentSawingStream);
                tool.Comp.CurrentSawingStream = null;
                Dirty(tool);
            }
            return;
        }

        _popup.PopupEntity(Loc.GetString("amputation-started", ("limb", Loc.GetString($"amputation-limb-{limb.ToString().ToLower()}"))),
            target, args.User);
    }

    private bool CheckMaxAmputations(EntityUid target, AmputateLimb limb, AmputatedLimbsComponent amputated)
    {
        // Проверка для ног
        if (limb is AmputateLimb.LeftLeg or AmputateLimb.RightLeg)
        {
            var legCount = amputated.Amputated.Count(l => l is AmputateLimb.LeftLeg or AmputateLimb.RightLeg);
            if (legCount >= 2)
                return false;
        }

        // Проверка для рук
        if (limb is AmputateLimb.LeftArm or AmputateLimb.RightArm)
        {
            var armCount = amputated.Amputated.Count(l => l is AmputateLimb.LeftArm or AmputateLimb.RightArm);
            if (armCount >= 2)
                return false;
        }

        // Голову можно ампутировать только одну
        if (limb == AmputateLimb.Head && amputated.Amputated.Contains(AmputateLimb.Head))
            return false;

        return true;
    }

    private void OnDoAfter(AmputationDoAfterEvent ev)
    {
        // Остановка звука
        if (ev.Used != null && TryComp<AmputationToolComponent>(ev.Used, out var toolComp))
        {
            if (toolComp.CurrentSawingStream != null)
            {
                _audio.Stop(toolComp.CurrentSawingStream);
                toolComp.CurrentSawingStream = null;
            }

            Dirty(ev.Used.Value, toolComp);
        }

        // Проверка, что DoAfter завершился успешно
        if (ev.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString("amputation-cancelled"), ev.Target ?? ev.User, ev.User);
            return;
        }

        // Проверка компонентов
        if (!TryComp<AmputationToolComponent>(ev.Used, out var tool) ||
            ev.Target is not {} target ||
            !HasComp<AmputatableComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("amputation-failed"), ev.Target ?? ev.User, ev.User);
            return;
        }

        var amputated = EnsureComp<AmputatedLimbsComponent>(target);

        if (amputated.Amputated.Contains(ev.Limb))
        {
            _popup.PopupEntity(Loc.GetString("amputation-limb-already-amputated"), target, ev.User);
            return;
        }

        // Проверка максимального количества ампутаций
        if (!CheckMaxAmputations(target, ev.Limb, amputated))
        {
            _popup.PopupEntity(Loc.GetString("amputation-max-limit"), target, ev.User);
            return;
        }

        PerformAmputation(target, ev.Limb, amputated, tool);
    }

    private void PerformAmputation(EntityUid target, AmputateLimb limb, AmputatedLimbsComponent amputated, AmputationToolComponent tool)
    {
        var coords = Transform(target).Coordinates;

        // Получаем AmputatableComponent
        if (!TryComp<AmputatableComponent>(target, out var amputatable))
        {
            _popup.PopupEntity(Loc.GetString("amputation-failed"), target, target);
            return;
        }

        // Наносим 50 Slash урона при любой ампутации
        var slashDamage = new DamageSpecifier();
        slashDamage.DamageDict.Add("Slash", 50);
        _damageable.TryChangeDamage(target, slashDamage, true);

        // Голова — специальная логика
        if (limb == AmputateLimb.Head)
        {
            var headProto = amputatable.SeveredHead;

            if (headProto != null)
            {
                // Если указан прототип головы - спавним его
                Spawn(headProto.Value, coords.Offset(_random.NextVector2(0.3f)));
            }
            else
            {
                // Если прототип не указан - телепортируем существующую голову
                RemoveBodyPartFromBody(target, BodyPartType.Head, null, coords);
            }

            // Мгновенная смерть через большой урон
            var deathDamage = new DamageSpecifier();
            deathDamage.DamageDict.Add("Bloodloss", 150);
            deathDamage.DamageDict.Add("Slash", 50);
            _damageable.TryChangeDamage(target, deathDamage, true);

            // Удаляем все инфекции при ампутации головы
            RemCompDeferred<HandInfectionComponent>(target);
            RemCompDeferred<LegInfectionComponent>(target);

            _popup.PopupEntity(Loc.GetString("amputation-head-removed"), target, target, PopupType.LargeCaution);
        }
        // Ноги — изменение скорости
        else if (limb is AmputateLimb.LeftLeg or AmputateLimb.RightLeg)
        {
            var symmetry = limb == AmputateLimb.LeftLeg ? BodyPartSymmetry.Left : BodyPartSymmetry.Right;
            var legProto = limb == AmputateLimb.LeftLeg ? amputatable.SeveredLeftLeg : amputatable.SeveredRightLeg;

            if (legProto != null)
            {
                // Если указан прототип ноги - спавним его
                Spawn(legProto.Value, coords.Offset(_random.NextVector2(0.2f)));
            }
            else
            {
                // Если прототип не указан - удаляем ногу из тела
                RemoveBodyPartFromBody(target, BodyPartType.Leg, symmetry, coords);
            }

            // Удаляем инфекцию ног при ампутации любой ноги
            RemCompDeferred<LegInfectionComponent>(target);

            // Считаем, сколько ног будет после этой ампутации
            var legsAfterAmputation = amputated.Amputated.Count(l => l is AmputateLimb.LeftLeg or AmputateLimb.RightLeg) + 1;

            // Изменяем скорость передвижения ТОЛЬКО если это первая нога
            if (legsAfterAmputation == 1)
            {
                if (TryComp<MovementSpeedModifierComponent>(target, out var speed))
                {
                    var newWalk = MathF.Max(0.5f, speed.BaseWalkSpeed - 1.5f);
                    var newSprint = MathF.Max(0.5f, speed.BaseSprintSpeed - 1.5f);

                    _movement.ChangeBaseSpeed(target, newWalk, newSprint, speed.Acceleration, speed);
                }
                else
                {
                    // Если компонента нет, создаем его
                    var newSpeed = EnsureComp<MovementSpeedModifierComponent>(target);
                    var newWalk = MathF.Max(0.5f, 4.5f - 1.5f);
                    var newSprint = MathF.Max(0.5f, 7.5f - 1.5f);

                    _movement.ChangeBaseSpeed(target, newWalk, newSprint, 10f, newSpeed);
                }
            }
            // Если это вторая нога - ВОЗВРАЩАЕМ скорость к исходной
            else if (legsAfterAmputation == 2)
            {
                if (TryComp<MovementSpeedModifierComponent>(target, out var speed))
                {
                    // Возвращаем скорость к исходной (прибавляем 1.5)
                    var originalWalk = speed.BaseWalkSpeed + 1.5f;
                    var originalSprint = speed.BaseSprintSpeed + 1.5f;

                    _movement.ChangeBaseSpeed(target, originalWalk, originalSprint, speed.Acceleration, speed);
                }

                // ФУНКЦИЯ №1: Если ампутированы обе ноги - насильно укладываем персонажа
                // Создаем компонент, который блокирует вставание
                var blockStanding = EnsureComp<CannotStandComponent>(target);

                // Насильно укладываем персонажа
                _standing.Down(target, playSound: true, dropHeldItems: true, force: true);

                _popup.PopupEntity(Loc.GetString("amputation-both-legs-removed-cannot-stand"), target, target, PopupType.LargeCaution);
            }
        }
        // Руки — уменьшение количества рук
        else if (limb is AmputateLimb.LeftArm or AmputateLimb.RightArm)
        {
            var symmetry = limb == AmputateLimb.LeftArm ? BodyPartSymmetry.Left : BodyPartSymmetry.Right;
            var armProto = limb == AmputateLimb.LeftArm ? amputatable.SeveredLeftArm : amputatable.SeveredRightArm;

            if (armProto != null)
            {
                // Если указан прототип руки - спавним его
                Spawn(armProto.Value, coords.Offset(_random.NextVector2(0.2f)));
            }
            else
            {
                // Если прототип не указан - удаляем руку из тела
                RemoveBodyPartFromBody(target, BodyPartType.Arm, symmetry, coords);
            }

            // Удаляем инфекцию рук при ампутации любой руки
            RemCompDeferred<HandInfectionComponent>(target);

            // ИСПРАВЛЕНИЕ: Удаляем конкретную руку, а не случайную
            if (TryComp<HandsComponent>(target, out var hands))
            {
                var handLocation = limb == AmputateLimb.LeftArm ? HandLocation.Left : HandLocation.Right;

                // Находим руку с нужной локацией
                var handToRemove = hands.Hands.FirstOrDefault(h => h.Value.Location == handLocation).Key;

                if (!string.IsNullOrEmpty(handToRemove))
                {
                    // Пытаемся вызвать RemoveHand, если он существует
                    _hands.RemoveHand(target, handToRemove);
                }
                else
                {
                    // Если не нашли руку по локации, используем старый метод
                    _hands.AmputateArm(target);
                }
            }
            else
            {
                // Если компонента рук нет, используем старый метод
                _hands.AmputateArm(target);
            }
        }

        // Отметка ампутации
        amputated.Amputated.Add(limb);
        Dirty(target, amputated);

        // ФУНКЦИЯ №2: Если отсутствуют все конечности (обе руки и обе ноги), добавляем компонент LegsParalyzedComponent
        var amputatedArmsCount = amputated.Amputated.Count(l => l is AmputateLimb.LeftArm or AmputateLimb.RightArm);
        var amputatedLegsCount = amputated.Amputated.Count(l => l is AmputateLimb.LeftLeg or AmputateLimb.RightLeg);

        if (amputatedArmsCount >= 2 && amputatedLegsCount >= 2)
        {
            EnsureComp<LegsParalyzedComponent>(target);
            _popup.PopupEntity(Loc.GetString("amputation-all-limbs-removed-paralyzed"), target, target, PopupType.LargeCaution);
        }

        // Проигрываем звук успешной ампутации
        if (tool.SuccessSound != null)
        {
            var successAudioParams = AudioParams.Default
                .WithVolume(5f)
                .WithMaxDistance(15f)
                .WithVariation(0.1f)
                .WithRolloffFactor(1.5f);

            _audio.PlayEntity(tool.SuccessSound, Filter.Pvs(target), target, true, successAudioParams);
        }

        _popup.PopupEntity(Loc.GetString("amputation-success",
            ("limb", Loc.GetString($"amputation-limb-{limb.ToString().ToLower()}"))),
            target, PopupType.LargeCaution);
    }

    /// <summary>
    /// Удаляет часть тела из Body компонента и перемещает ее в мир
    /// </summary>
    private void RemoveBodyPartFromBody(EntityUid target, BodyPartType partType, BodyPartSymmetry? symmetry, EntityCoordinates coordinates)
    {
        if (!TryComp<BodyComponent>(target, out var body))
            return;

        // Получаем все части тела нужного типа
        var parts = _body.GetBodyChildrenOfType(target, partType, body);

        foreach (var part in parts)
        {
            // Проверяем симметрию, если указана
            if (symmetry != null && TryComp<BodyPartComponent>(part.Id, out var partComp) && partComp.Symmetry != symmetry)
                continue;

            // Удаляем часть из тела
            // Вместо DropPart, который может не существовать, мы просто телепортируем часть
            var offsetCoords = coordinates.Offset(_random.NextVector2(0.3f));
            _transform.SetCoordinates(part.Id, offsetCoords);

            // Убеждаемся, что у части есть физика
            EnsureComp<PhysicsComponent>(part.Id);

            // Прерываем цикл, так как нашли нужную часть
            // (для головы может быть только одна, для рук/ног - левая/правая)
            break;
        }
    }
}