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

        var limbs = new List<string>();
        foreach (var limb in ent.Comp.Amputated)
        {
            limbs.Add(Loc.GetString($"amputation-limb-{limb.ToString().ToLower()}"));
        }

        var text = Loc.GetString("amputation-examine", ("limbs", string.Join(", ", limbs)));
        args.PushText(text);
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

        // ИСПРАВЛЕНИЕ: ВСЕГДА проигрываем звук, так как задержка всегда есть (7 секунд)
        var audioParams = AudioParams.Default
            .WithVolume(7f)
            .WithLoop(true)
            .WithMaxDistance(10f)
            .WithVariation(0.1f)
            .WithRolloffFactor(1.5f);

        // ИСПРАВЛЕНИЕ: Используем PlayEntity для гарантированного проигрывания на сущности
        var played = _audio.PlayEntity(tool.Comp.SawSound, Filter.Pvs(tool.Owner), tool.Owner, true, audioParams);

        if (played != null)
        {
            tool.Comp.CurrentSawingStream = played.Value.Entity;
            Dirty(tool);
        }
        else
        {
            // Отладочное сообщение
            _popup.PopupEntity("DEBUG: Sound playback failed!", tool.Owner, args.User);
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
        if (ev.Used != null && TryComp<AmputationToolComponent>(ev.Used, out var tool))
        {
            if (tool.CurrentSawingStream != null)
                _audio.Stop(tool.CurrentSawingStream);

            tool.CurrentSawingStream = null;
            Dirty(ev.Used.Value, tool);
        }

        // Проверка, что DoAfter завершился успешно
        if (ev.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString("amputation-cancelled"), ev.Target ?? ev.User, ev.User);
            return;
        }

        // Проверка компонентов
        if (!TryComp<AmputationToolComponent>(ev.Used, out var toolComp) ||
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

        PerformAmputation(target, ev.Limb, amputated);
    }

    private void PerformAmputation(EntityUid target, AmputateLimb limb, AmputatedLimbsComponent amputated)
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

        // Голова — специальная логика (но с процессбаром)
        if (limb == AmputateLimb.Head)
        {
            // Если указан прототип головы - спавним его
            if (amputatable.SeveredHead != null)
            {
                Spawn(amputatable.SeveredHead.Value, coords.Offset(_random.NextVector2(0.3f)));
            }
            else
            {
                // Если прототип не указан - телепортируем существующую голову
                if (TryComp<BodyComponent>(target, out var body))
                {
                    var heads = _body.GetBodyChildrenOfType(target, BodyPartType.Head, body);
                    foreach (var head in heads)
                    {
                        var offsetCoords = coords.Offset(_random.NextVector2(0.3f));
                        _transform.SetCoordinates(head.Id, offsetCoords);
                        EnsureComp<PhysicsComponent>(head.Id);
                    }
                }
            }

            // Мгновенная смерть через большой урон
            var deathDamage = new DamageSpecifier();
            deathDamage.DamageDict.Add("Bloodloss", 150);
            deathDamage.DamageDict.Add("Slash", 50);
            _damageable.TryChangeDamage(target, deathDamage, true);

            _popup.PopupEntity(Loc.GetString("amputation-head-removed"), target, target, PopupType.LargeCaution);
        }
        else
        {
            // Для остальных конечностей - спавн отрезанной части если указан прототип
            var proto = limb switch
            {
                AmputateLimb.LeftArm => amputatable.SeveredLeftArm,
                AmputateLimb.RightArm => amputatable.SeveredRightArm,
                AmputateLimb.LeftLeg => amputatable.SeveredLeftLeg,
                AmputateLimb.RightLeg => amputatable.SeveredRightLeg,
                _ => null
            };

            if (proto != null)
                Spawn(proto.Value, coords.Offset(_random.NextVector2(0.2f)));

            // Ноги — изменение скорости
            if (limb is AmputateLimb.LeftLeg or AmputateLimb.RightLeg)
            {
                if (TryComp<MovementSpeedModifierComponent>(target, out var speed))
                {
                    // Вычитаем 1.5 из текущих значений
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
            // Руки — уменьшение количества рук
            else if (limb is AmputateLimb.LeftArm or AmputateLimb.RightArm)
            {
                // Используем метод из HandsSystem
                _hands.AmputateArm(target);
            }
        }

        // Лечение инфекции
        RemCompDeferred<PendingInfectionComponent>(target);

        // Отметка ампутации
        amputated.Amputated.Add(limb);
        Dirty(target, amputated);

        _popup.PopupEntity(Loc.GetString("amputation-success",
            ("limb", Loc.GetString($"amputation-limb-{limb.ToString().ToLower()}"))),
            target, PopupType.LargeCaution);
    }
}