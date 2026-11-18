using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils.Json;

namespace Devil.Server;

[Injectable(TypePriority = OnLoadOrder.PostLoad + 100)]
public sealed class CultistWaves(LocationTable locations, ISptLogger<CultistWaves> logger) : IOnLoad
{
    private const string Log = "[Devil/Cult]";
    private const string SptIdPrefix = "devil-cult-";

    private const double GroupChance = 100;

    private sealed record MapWave(string Map, string Zone, string EscortAmount, bool RandomTime);

    private static readonly MapWave[] Waves =
    {
        new("shoreline", "ZoneMeteoStation,ZoneSanatorium1,ZoneSanatorium2,ZoneForestSpawn", "0", false),
        new("woods", "ZoneScavBase2,ZoneMiniHouse,ZoneBrokenVill,ZoneStoneBunker", "2", false),
        new("bigmap", "ZoneScavBase,ZoneDormitory", "2", false),
    };

    private static void EnsureSupport(BossLocationSpawn wave, string botType)
    {
        var supports = wave.Supports?.ToList() ?? new List<BossSupport>();

        if (supports.Any(s =>
            string.Equals(s.BossEscortType, botType, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        supports.Add(new BossSupport
        {
            BossEscortType = botType,
            BossEscortDifficulty = new ListOrT<string>(
                new List<string> { "normal" }, null),
            BossEscortAmount = "1"
        });

        wave.Supports = supports;
    }

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        logger.Info($"{Log} CultistWaves OnLoadAsync started.");

        logger.Info($"{Log} GroupChance = {GroupChance}%.");
        var added = new List<string>();

        foreach (var w in Waves)
        {
            try
            {
                var spawns = locations.GetLocation(w.Map)?.Base?.BossLocationSpawn;
                if (spawns == null)
                {
                    logger.Warning($"{Log} map '{w.Map}' not found, skipped.");
                    continue;
                }

                spawns.RemoveAll(s => s?.SptId == SptIdPrefix + w.Map);

                var existing = spawns.FirstOrDefault(s => string.Equals(s?.BossName, "sectantPredvestnik", StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    existing.BossChance = GroupChance;

                    existing.Supports ??= new List<BossSupport>();

                    EnsureSupport(existing, "sectantOni");
                    EnsureSupport(existing, "sectantPrizrak");
                    EnsureSupport(existing, "bossDevil");

                    logger.Info(
                        $"{Log} Harbinger diagnostics '{w.Map}': " +
                        $"Chance={existing.BossChance}, " +
                        $"Zone={existing.BossZone}, " +
                        $"EscortAmount={existing.BossEscortAmount}, " +
                        $"EscortType={existing.BossEscortType}, " +
                        $"Supports={string.Join(", ", existing.Supports?.Select(s =>
                            $"{s.BossEscortType} x{s.BossEscortAmount}") ?? Enumerable.Empty<string>())}, " +
                        $"RandomTime={existing.IsRandomTimeSpawn}, " +
                        $"Time={existing.Time}, " +
                        $"ForceSpawn={existing.ForceSpawn}");

                    added.Add(w.Map);

                    continue;
                }


                var newWave = Build(w);

                spawns.Add(newWave);
                added.Add(w.Map);
            }
            catch (Exception ex)
            {
                logger.Warning($"{Log} couldn't add the cult to {w.Map}: {ex.Message}");
            }
        }

        logger.Info($"{Log} added to {added.Count} map(s): {string.Join(", ", added)} ({GroupChance:0}% chance).");
        return Task.CompletedTask;
    }

    private static BossLocationSpawn Build(MapWave w) => new()
    {
        SptId = SptIdPrefix + w.Map,
        BossName = "sectantPredvestnik",
        BossChance = GroupChance,
        BossDifficulty = "normal",
        BossEscortAmount = w.EscortAmount,
        BossEscortDifficulty = "normal",
        BossEscortType = "exUsec",
        BossZone = w.Zone,
        IsBossPlayer = false,
        IsRandomTimeSpawn = w.RandomTime,
        Time = -1,
        Delay = 0,
        TriggerId = "",
        TriggerName = "",
        ForceSpawn = false,
        IgnoreMaxBots = true,
        SpawnMode = new List<string> { "regular", "pve" },
        Supports = new List<BossSupport>
        {
            new() { BossEscortType = "sectantOni",     BossEscortDifficulty = new ListOrT<string>(new List<string> { "normal" }, null), BossEscortAmount = "1" },
            new() { BossEscortType = "sectantPrizrak", BossEscortDifficulty = new ListOrT<string>(new List<string> { "normal" }, null), BossEscortAmount = "1" },
            new() { BossEscortType = "bossDevil", BossEscortDifficulty = new ListOrT<string>(new List<string> { "normal" }, null), BossEscortAmount = "1" }
        },
    };
}