using Content.Shared.Administration;
using Robust.Shared.Console;
using Content.Shared.ADT.Sanity;

namespace Content.Server.ADT.Sanity;

[AnyCommand]
public sealed class SanityCommand : IConsoleCommand
{
    public string Command => "sanity";
    public string Description => "Управление рассудком";
    public string Help => "sanity <get/set> <uid> [value]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteLine(Help);
            return;
        }

        var entManager = IoCManager.Resolve<IEntityManager>();

        if (!EntityUid.TryParse(args[1], out var uid))
        {
            shell.WriteLine("Неверный UID");
            return;
        }

        if (!entManager.TryGetComponent<SanityComponent>(uid, out var sanity))
        {
            shell.WriteLine("У ентити нет компонента рассудка");
            return;
        }

        switch (args[0].ToLower())
        {
            case "get":
                shell.WriteLine($"Рассудок: {sanity.CurrentSanity}/100");
                break;

            case "set":
                if (args.Length < 3 || !float.TryParse(args[2], out var value))
                {
                    shell.WriteLine("Использование: sanity set <uid> <value>");
                    return;
                }

                sanity.CurrentSanity = Math.Clamp(value, 0, 100);
                shell.WriteLine($"Рассудок установлен: {sanity.CurrentSanity}/100");
                break;

            default:
                shell.WriteLine("Неизвестная команда. Используйте get или set");
                break;
        }
    }
}