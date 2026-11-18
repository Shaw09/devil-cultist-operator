using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace Devil.Server;

[Injectable(TypePriority = OnLoadOrder.PostLoad + 150)]
public sealed class PriestKnife(BotTable bots, ISptLogger<PriestKnife> logger) : IOnLoad
{
    private const string Log = "[Devil/Knife]";

    public const string KnifeId = "6ae9c382f729bf328e2c16e2";

    private const double Chance = 0.10;

    private static readonly string[] Roles = { "sectantpriest" };

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var knife = new MongoId(KnifeId);

        foreach (var role in Roles)
        {
            if (!bots.Types.TryGetValue(role, out var type) || type?.BotInventory?.Equipment == null)
            {
                logger.Warning($"{Log} bot type '{role}' not found, skipped.");
                continue;
            }

            if (!type.BotInventory.Equipment.TryGetValue(EquipmentSlots.Scabbard, out var pool) || pool == null)
            {
                logger.Warning($"{Log} '{role}' has no Scabbard pool, skipped.");
                continue;
            }

            pool.Remove(knife);
            var total = pool.Values.Sum();
            if (total <= 0)
            {
                logger.Warning($"{Log} '{role}' Scabbard pool is empty, skipped.");
                continue;
            }

            pool[knife] = total * Chance / (1 - Chance);
            logger.Info($"{Log} {role}: knife added to Scabbard ({Chance:P0}).");
        }

        return Task.CompletedTask;
    }
}