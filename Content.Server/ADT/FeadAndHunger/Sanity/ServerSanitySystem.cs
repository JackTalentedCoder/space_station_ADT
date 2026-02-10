using Content.Server.Chat.Managers;
using Content.Shared.ADT.Sanity;
using Content.Shared.ADT.Sanity.Components;
using Content.Shared.ADT.Sanity.EntitySystems;
using Content.Shared.Chat;
using Robust.Server.GameObjects;
using Robust.Shared.Player;

namespace Content.Server.ADT.Sanity.EntitySystems;

public sealed class ServerSanitySystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chat = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SanityComponent, ShowSanityEvent>(OnShowSanity);
    }

    private void OnShowSanity(Entity<SanityComponent> entity, ref ShowSanityEvent args)
    {
        if (args.Handled)
            return;

        // Получаем актора (игрока) для сущности
        if (!TryComp<ActorComponent>(entity, out var actor))
            return;

        // Получаем текущий рассудок через shared систему
        var sanitySystem = EntityManager.System<SanitySystem>();
        var currentSanity = sanitySystem.GetSanity(entity.Comp);

        // Создаем сообщение
        var message = Loc.GetString("sanity-current", ("sanity", (int)currentSanity));

        // Отправляем сообщение в чат ЛОКАЛЬНО для конкретного игрока
        // Используем ChatMessageToManyFiltered как в WeatherSchedulerSystem, но с фильтром для одного игрока
        var filter = Filter.SinglePlayer(actor.PlayerSession);

        _chat.ChatMessageToManyFiltered(
            filter,
            ChatChannel.Local,
            message,
            message,
            entity.Owner,
            false,
            true,
            null);

        args.Handled = true;
    }
}