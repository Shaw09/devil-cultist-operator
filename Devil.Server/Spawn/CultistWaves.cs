
using Devil.Server.Controller;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils.Json;

namespace Devil.Server;

[Injectable(
    InjectionType = InjectionType.Singleton,
    TypePriority = OnLoadOrder.PostLoad + 100)]
public sealed class CultistWaves(
    LocationTable locations,
    DevilServerConfigService configService,
    ISptLogger<CultistWaves> logger) : IOnLoad
{
    private const string Log = "[Devil/Cult]";
    private const string SptIdPrefix = "devil-cult-";
    private const string HarbingerName = "sectantPredvestnik";
    private const string EscortType = "sectantWarrior";

    private sealed record MapWave(
        string Map,
        string Zone,
        int MaxEscort,
        bool RandomTime
    );

    private static readonly MapWave[] Waves =
    {
        new(
            "shoreline",
            "ZoneMeteoStation,ZoneSanatorium1,ZoneSanatorium2,ZoneForestSpawn",
            6,
            false
        ),
        new(
            "woods",
            "ZoneScavBase2,ZoneMiniHouse,ZoneBrokenVill,ZoneStoneBunker",
            4,
            false
        ),
        new(
            "bigmap",
            "ZoneScavBase,ZoneDormitory",
            4,
            false
        )
    };

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        logger.Info($"{Log} CultistWaves initialized.");

        // Load the shared configuration.
        var cfg = configService.Initialize();

        // Subscribe to future configuration changes.
        configService.Changed += OnConfigChanged;

        ApplyWaves(cfg, "server start");

        return Task.CompletedTask;
    }

    private void OnConfigChanged(DevilServerConfig cfg)
    {
        logger.Info($"{Log} Configuration changed. Rebuilding waves.");

        ApplyWaves(cfg, "config changed");
    }

    /// <summary>True for the Cult Gang (Harbinger) waves this mod adds.</summary>
    public static bool IsCultWave(BossLocationSpawn? spawn) =>
        spawn?.SptId != null && spawn.SptId.StartsWith(SptIdPrefix, StringComparison.Ordinal);

    /// <summary>
    /// Builds a fresh Cult Gang wave for one map (new escort roll), or null if the map has no Cult Gang wave.
    /// Used by <see cref="RaidWaveGuardPatch"/>.
    /// </summary>
    public BossLocationSpawn? CreateWaveForMap(string map)
    {
        var wave = Waves.FirstOrDefault(w => string.Equals(w.Map, map, StringComparison.OrdinalIgnoreCase));
        if (wave == null)
        {
            return null;
        }

        var cfg = configService.Current;
        return Build(wave, cfg, CalculateEscortCount(cfg, wave));
    }

    private static double ClampChance(double value)
    {
        if (!double.IsFinite(value))
            return 100;

        return Math.Clamp(value, 0, 100);
    }

    private static double GetEscortChance(
    DevilServerConfig cfg,
    MapWave wave)
    {
        foreach (var entry in cfg.CultGangEscortSpawnChance)
        {
            if (string.Equals(
                entry.Key,
                wave.Map,
                StringComparison.OrdinalIgnoreCase))
            {
                return ClampChance(entry.Value);
            }
        }

        return 100;
    }

    /// <summary>
    /// Calculates the number of escorts.
    /// First rolls the chance of having escorts.
    /// Then uses a weighted random distribution favoring larger groups.
    /// </summary>
    private static int CalculateEscortCount(
        DevilServerConfig cfg,
        MapWave wave)
    {
        int max = wave.MaxEscort;

        if (max <= 0)
            return 0;

        double chance = GetEscortChance(cfg, wave);

        // First roll: will the group have escorts?
        if (Random.Shared.NextDouble() * 100 >= chance)
            return 0;

        // Second roll: determine the number of escorts.
        // Higher quantities receive greater weight.
        int totalWeight = 0;

        for (int i = 1; i <= max; i++)
            totalWeight += i * i;

        int roll = Random.Shared.Next(totalWeight);

        for (int i = 1; i <= max; i++)
        {
            int weight = i * i;

            if (roll < weight)
                return i;

            roll -= weight;
        }

        return max;
    }

    private static BossSupport Support(string type, string amount) => new()
    {
        BossEscortType = type,
        BossEscortDifficulty = new ListOrT<string>(
            new List<string> { "impossible" },
            null
        ),
        BossEscortAmount = amount
    };

    private static BossLocationSpawn Build(
        MapWave wave,
        DevilServerConfig cfg,
        int escort)
    {
        var supports = new List<BossSupport>();

        if (cfg.Oni)
            supports.Add(Support("sectantOni", "1"));

        if (cfg.Prizrak)
            supports.Add(Support("sectantPrizrak", "1"));

        if (escort > 0)
            supports.Add(Support(EscortType, escort.ToString()));

        return new BossLocationSpawn
        {
            SptId = SptIdPrefix + wave.Map,

            BossName = HarbingerName,
            BossChance = ClampChance(cfg.SpawnChanceCultGang),
            BossDifficulty = "impossible",

            BossEscortAmount = escort.ToString(),
            BossEscortDifficulty = "impossible",
            BossEscortType = EscortType,

            BossZone = wave.Zone,

            IsBossPlayer = false,
            IsRandomTimeSpawn = wave.RandomTime,

            Time = -1,
            Delay = 0,

            TriggerId = "",
            TriggerName = "",

            ForceSpawn = false,
            IgnoreMaxBots = true,

            SpawnMode = new List<string>
            {
                "regular",
                "pve"
            },

            Supports = supports
        };
    }

    private void ApplyWaves(
        DevilServerConfig cfg,
        string reason)
    {
        lock (DevilServerConfigService.WaveLock)
        {
            logger.Info(
                $"{Log} Applying waves ({reason}). " +
                $"CultGangChance={cfg.SpawnChanceCultGang}%"
            );

            var added = new List<string>();

            foreach (var wave in Waves)
            {
                try
                {
                    var spawns = locations
                        .GetLocation(wave.Map)?
                        .Base?
                        .BossLocationSpawn;

                    if (spawns == null)
                    {
                        logger.Warning(
                            $"{Log} Map '{wave.Map}' not found."
                        );
                        continue;
                    }

                    // Remove previous Devil Cult Gang waves.
                    int removed = spawns.RemoveAll(s =>
                        s != null &&
                        s.SptId == SptIdPrefix + wave.Map
                    );

                    if (removed > 0)
                    {
                        logger.Info(
                            $"{Log} '{wave.Map}': removed {removed} previous wave(s)."
                        );
                    }

                    int escort = CalculateEscortCount(cfg, wave);

                    var newWave = Build(wave, cfg, escort);

                    spawns.Add(newWave);
                    added.Add(wave.Map);

                    logger.Info(
                        $"{Log} '{wave.Map}': wave created. " +
                        $"Chance={newWave.BossChance}%, " +
                        $"EscortChance={GetEscortChance(cfg, wave)}%, " +
                        $"EscortCount={escort}/{wave.MaxEscort}, " +
                        $"Oni={cfg.Oni}, Prizrak={cfg.Prizrak}"
                    );
                }
                catch (Exception ex)
                {
                    logger.Warning(
                        $"{Log} Failed to create wave on '{wave.Map}': {ex.Message}"
                    );
                }
            }

            logger.Info(
                $"{Log} Waves applied to {added.Count} maps: " +
                string.Join(", ", added)
            );
        }
    }
}
