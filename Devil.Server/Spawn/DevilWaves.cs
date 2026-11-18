using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace Devil.Server;

[Injectable(TypePriority = OnLoadOrder.PostLoad + 100)]
public sealed class DevilWaves(LocationTable locations, ISptLogger<DevilWaves> logger) : IOnLoad
{
    private const string Log = "[Devil]";
    public const string SptId = "devil-cultist-operator";

    private const double SpawnChance = 100;

    private static readonly string[] Maps = { "woods", "bigmap", "shoreline", "rezervbase", "tarkovstreets", "lighthouse" };

    private static readonly Dictionary<string, string> DevilZones = new()
    {
        ["bigmap"] = "ZoneDormitory,ZoneDormitory,ZoneDormitory,ZoneScavBase",
        ["shoreline"] = "ZoneSanatorium1,ZoneSanatorium2,ZoneForestSpawn, ZoneMeteoStation",
        ["woods"] = "ZoneStoneBunker,ZoneStoneBunker,ZoneStoneBunker,ZoneMiniHouse,ZoneBrokenVill, ZoneScavBase2",
        ["rezervbase"] = "ZoneBarrack,ZoneSubCommand,ZoneBunkerStorage",
        ["tarkovstreets"] = "ZoneFactory,ZoneClimova,ZoneMvd,ZoneConstruction",
        ["lighthouse"] = "Zone_Hellicopter"
    };

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var added = new List<string>();

        foreach (var map in Maps)
        {
            try
            {
                var loc = locations.GetLocation(map)?.Base;
                var spawns = loc?.BossLocationSpawn;
                if (loc == null || spawns == null)
                {
                    logger.Warning($"{Log} map '{map}' not found, skipped.");
                    continue;
                }

                var zone = DevilZones.TryGetValue(map, out var z) ? z : Zones(loc);
                if (string.IsNullOrEmpty(zone))
                {
                    logger.Warning($"{Log} no usable zone on '{map}', skipped.");
                    continue;
                }

                spawns.RemoveAll(s => s?.SptId == SptId);
                spawns.Add(Wave(zone));
                added.Add(map);
            }
            catch (Exception ex)
            {
                logger.Warning($"{Log} couldn't add Devil to {map}: {ex.Message}");
            }
        }

        logger.Info($"{Log} added to {added.Count} map(s): {string.Join(", ", added)} ({SpawnChance:0}% chance).");
        return Task.CompletedTask;
    }

    private static string Zones(LocationBase loc)
    {
        var zones = (loc.OpenZones ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(z => z.IndexOf("snipe", StringComparison.OrdinalIgnoreCase) < 0)
            .Distinct()
            .ToList();

        if (zones.Count == 0)
        {
            var other = loc.BossLocationSpawn?.FirstOrDefault(b => !string.IsNullOrEmpty(b?.BossZone) && b.SptId != SptId);
            if (other != null) return other.BossZone!;
        }

        return string.Join(",", zones);
    }

    private static BossLocationSpawn Wave(string zone) => new()
    {
        SptId = SptId,
        BossName = ModInfo.DevilBotKey,
        BossChance = SpawnChance,
        BossDifficulty = "normal",
        BossEscortAmount = "0",
        BossEscortDifficulty = "normal",
        BossEscortType = "sectantWarrior",
        BossZone = zone,
        IsBossPlayer = false,
        IsRandomTimeSpawn = false,
        Time = -1,
        Delay = 0,
        TriggerId = "",
        TriggerName = "",
        Supports = null!,
        ForceSpawn = false,
        IgnoreMaxBots = true,
        SpawnMode = new List<string> { "regular", "pve" },
    };
}