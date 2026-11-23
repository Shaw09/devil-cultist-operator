
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Devil.Server.Controller;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace Devil.Server;

[Injectable(InjectionType = InjectionType.Singleton, TypePriority = OnLoadOrder.PostLoad + 100)]
public sealed class DevilWaves(
    LocationTable locations,
    DevilServerConfigService configService,
    ISptLogger<DevilWaves> logger) : IOnLoad
{
    private const string Log = "[Devil]";
    public const string SptId = "devil-cultist-operator";

    private const string EscortType = "sectantWarrior";
    private const double ClientGateBossChance = 100;

    private sealed record MapWave(string Map, string? Zone, int MaxEscort);

    private static readonly MapWave[] Waves =
    {
        new("woods",         "ZoneStoneBunker,ZoneStoneBunker,ZoneStoneBunker,ZoneMiniHouse,ZoneBrokenVill,ZoneScavBase2", 2),
        new("bigmap",        "ZoneDormitory,ZoneDormitory,ZoneDormitory,ZoneScavBase",                                     4),
        new("shoreline",     "ZoneSanatorium1,ZoneSanatorium2,ZoneForestSpawn,ZoneMeteoStation",                           2),
        new("rezervbase",    "ZoneBarrack,ZoneSubCommand,ZoneBunkerStorage",                                               4),
        new("tarkovstreets", "ZoneFactory,ZoneClimova,ZoneMvd,ZoneConstruction",                                           10),
        new("lighthouse",    "Zone_Hellicopter",                                                                           6),
    };

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var config = configService.Initialize();

        configService.Changed += newConfig =>
            ApplyWaves(newConfig, "config reload");

        ApplyWaves(config, "server start");

        return Task.CompletedTask;
    }

    private void ApplyWaves(DevilServerConfig config, string reason)
    {
        lock (DevilServerConfigService.WaveLock)
        {
            double baseChance = Math.Clamp(config.SpawnChanceDevil, 0, 100);
            double bonusChance = Math.Clamp(config.DevilHarbingerBonusChance, 0, 100);

            logger.Info(
                $"{Log} Applying waves ({reason}). " +
                $"BaseChance={baseChance}%, " +
                $"HarbingerBonus={bonusChance}%, " +
                $"EngineBossChance={ClientGateBossChance}%.");

            var added = new List<string>();

            foreach (var wave in Waves)
            {
                try
                {
                    var location = locations.GetLocation(wave.Map)?.Base;
                    var spawns = location?.BossLocationSpawn;

                    if (location == null || spawns == null)
                    {
                        logger.Warning(
                            $"{Log} map '{wave.Map}' not found, skipped.");

                        continue;
                    }

                    var zone = CleanZones(wave.Zone) ?? Zones(location);

                    if (string.IsNullOrEmpty(zone))
                    {
                        logger.Warning(
                            $"{Log} no usable zone on '{wave.Map}', skipped.");

                        continue;
                    }

                    int escortCount = CalculateEscortCount(
                        config,
                        wave,
                        out double escortChance);

                    spawns.RemoveAll(s => s?.SptId == SptId);

                    spawns.Add(Build(zone, escortCount.ToString()));

                    added.Add(wave.Map);

                    logger.Info(
                        $"{Log} '{wave.Map}': " +
                        $"EngineBossChance={ClientGateBossChance}%, " +
                        $"BaseChance={baseChance}%, " +
                        $"HarbingerBonus={bonusChance}%, " +
                        $"EscortChance={escortChance}%, " +
                        $"EscortCount={escortCount}/{wave.MaxEscort}");
                }
                catch (Exception ex)
                {
                    logger.Warning(
                        $"{Log} couldn't add Devil to '{wave.Map}': {ex.Message}");
                }
            }

            logger.Info(
                $"{Log} Waves applied to {added.Count} maps: " +
                $"{string.Join(", ", added)}.");
        }
    }

    private static int CalculateEscortCount(
        DevilServerConfig config,
        MapWave wave,
        out double chance)
    {
        int max = wave.MaxEscort;

        chance = config.DevilEscortSpawnChance.TryGetValue(
            wave.Map,
            out double configuredChance)
            ? Math.Clamp(configuredChance, 0, 100)
            : 100;

        if (max <= 0)
        {
            return 0;
        }

        if (Random.Shared.NextDouble() * 100 >= chance)
        {
            return 0;
        }

        int totalWeight = 0;

        for (int i = 1; i <= max; i++)
        {
            totalWeight += i * i;
        }

        int roll = Random.Shared.Next(totalWeight);

        for (int i = 1; i <= max; i++)
        {
            int weight = i * i;

            if (roll < weight)
            {
                return i;
            }

            roll -= weight;
        }

        return max;
    }

    private static string? CleanZones(string? zones)
    {
        if (string.IsNullOrWhiteSpace(zones))
        {
            return null;
        }

        return string.Join(",",
            zones.Split(',',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries));
    }

    private static string Zones(LocationBase location)
    {
        var zones = (location.OpenZones ?? "")
            .Split(',',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Where(z => z.IndexOf(
                "snipe", StringComparison.OrdinalIgnoreCase) < 0)
            .Distinct()
            .ToList();

        if (zones.Count == 0)
        {
            var other = location.BossLocationSpawn?
                .FirstOrDefault(b =>
                    !string.IsNullOrEmpty(b?.BossZone) &&
                    b.SptId != SptId);

            if (other != null)
            {
                return other.BossZone!;
            }
        }

        return string.Join(",", zones);
    }

    private static BossLocationSpawn Build(
        string zone,
        string escortAmount) => new()
        {
            SptId = SptId,
            BossName = ModInfo.DevilBotKey,
            BossChance = ClientGateBossChance,
            BossDifficulty = "impossible",
            BossEscortAmount = escortAmount,
            BossEscortDifficulty = "impossible",
            BossEscortType = EscortType,
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
            SpawnMode = new List<string> { "regular", "pve" }
        };
}