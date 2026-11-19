using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using HarmonyLib;

namespace Devil.Client.Spawn
{
    internal class DevilSpawn
    {
        public const string BossName = "bossDevil";
        private const string HarbingerName = "sectantPredvestnik";
        private const WildSpawnType DevilWildSpawnType = (WildSpawnType)Plugin.DevilWildSpawnTypeId;
        private const int NightStartHour = 22;
        private const int NightEndHour = 7;
        private static bool _supportDecisionMade;

        private static readonly System.Random Rng = new();
        private static ManualLogSource _log;
        private static MethodInfo _spawnMethod;

        private static double _raidRoll = -1;
        private static bool _raidIsNight;
        private static bool _soloSelected;
        private static bool _supportSelected;
        private static bool _devilSpawned;
        private static bool _devilSpawnAttempted;

        private static readonly HashSet<string> LoggedWaves =
            new(StringComparer.OrdinalIgnoreCase);

        public static bool Install(Harmony harmony, ManualLogSource log)
        {
            _log = log;

            _spawnMethod = AccessTools.GetDeclaredMethods(typeof(BotBossSpawn))
                .FirstOrDefault(m =>
                    m.Name == "Spawn" &&
                    m.GetParameters().Length == 3 &&
                    m.GetParameters()[0].ParameterType == typeof(BossLocationSpawn));

            if (_spawnMethod == null)
            {
                log.LogError("[Devil] BotBossSpawn.Spawn not found.");
                return false;
            }

            harmony.Patch(
                _spawnMethod,
                prefix: new HarmonyMethod(
                    typeof(DevilSpawn),
                    nameof(SpawnPrefix))
                {
                    priority = Priority.First
                });

            log.LogInfo("[Devil] Per-raid spawn controller installed.");

            return true;
        }

        private static bool Is(BossLocationSpawn wave, string name)
        {
            return wave != null &&
                string.Equals(
                    wave.BossName,
                    name,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureRaidDecision()
        {
            if (_raidRoll >= 0)
                return;

            _raidIsNight = IsNight();
            _raidRoll = Rng.NextDouble() * 100.0;

            _soloSelected =
                _raidIsNight && _raidRoll < Plugin.Config.NightChance;

            _log.LogInfo(
                $"[Devil] Raid roll={_raidRoll:0.0}, " +
                $"night={_raidIsNight}, " +
                $"solo={_soloSelected}");
        }

        private static void EnsureSupportDecision()
        {
            if (_supportDecisionMade)
                return;

            _supportDecisionMade = true;

            double chance = Plugin.Config.WithCultGangChance;
            if (_raidIsNight)
                chance += Plugin.Config.NightChance;

            chance = Math.Clamp(chance, 0, 100);

            double roll = Rng.NextDouble() * 100.0;
            _supportSelected = roll < chance;

            _log.LogInfo(
                $"[Devil] Support roll={roll:0.0}, " +
                $"chance={chance}, " +
                $"night={_raidIsNight}, " +
                $"selected={_supportSelected}");
        }

        private static bool SpawnPrefix(BotBossSpawn __instance, BossLocationSpawn wave, object[] __args, ref Task __result)
        {
            try
            {
                if (wave == null)
                    return true;

                bool devilWave = Is(wave, BossName);
                bool harbingerWave = Is(wave, HarbingerName);

                if (!devilWave && !harbingerWave)
                    return true;

                string key = $"{wave.BossName}|{wave.BossZone}";

                if (LoggedWaves.Add(key))
                {
                    _log.LogInfo(
                        $"[Devil] wave seen: {wave.BossName} " +
                        $"zone={wave.BossZone}");
                }

                EnsureRaidDecision();

                if (harbingerWave)
                {
                    if (!CultistSpawn.ShouldSpawn())
                    {
                        __result = Task.CompletedTask;

                        _log.LogInfo(
                            "[CultistGang] Autonomous spawn blocked."
                        );

                        return false;
                    }

                    EnsureSupportDecision();
                    var supports = wave.Supports;

                    if (supports == null)
                    {
                        _log.LogWarning(
                            "[Devil] Harbinger supports are null.");

                        return true;
                    }

                    if (!_supportSelected)
                    {
                        int removed = supports.Count(s =>
                            s.BossEscortType == DevilWildSpawnType);

                        wave.Supports = supports
                            .Where(s => s.BossEscortType != DevilWildSpawnType)
                            .ToArray();

                        _log.LogInfo(
                             $"[Devil] support removed. Removed={removed}");
                    }
                    else
                    {
                        bool found = supports.Any(s =>
                            s.BossEscortType == DevilWildSpawnType);

                        if (!found)
                        {
                            _log.LogWarning(
                                "[Devil] Support selected, but bossDevil " +
                                "is missing from Harbinger.Supports.");
                        }
                        else
                        {
                            _devilSpawnAttempted = true;

                            _log.LogInfo(
                                "[Devil] Devil selected as Harbinger support.");
                        }
                    }

                    return true;
                }

                if (devilWave)
                {
                    if (!_soloSelected || _devilSpawnAttempted)
                    {
                        __result = Task.CompletedTask;

                        _log.LogInfo(
                            $"[Devil] autonomous spawn blocked. " +
                            $"SoloSelected={_soloSelected}, " +
                            $"AlreadyAttempted={_devilSpawnAttempted}");

                        return false;
                    }

                    _devilSpawnAttempted = true;

                    ApplyOwnZones(__instance, wave);

                    _log.LogInfo(
                        "[Devil] autonomous spawn selected.");

                    return true;
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(
                    $"[Devil] SpawnPrefix error: {ex}");

                return true;
            }

            return true;
        }

        private static void ApplyOwnZones(
            BotBossSpawn spawner,
            BossLocationSpawn wave)
        {
            if (spawner?._allZones == null ||
                string.IsNullOrEmpty(wave?.BossZone))
                return;

            var pool = new List<BotZone>();

            foreach (var raw in wave.BossZone.Split(','))
            {
                var name = raw.Trim();

                if (name.Length == 0)
                    continue;

                var zone = spawner._allZones.FirstOrDefault(z =>
                    z != null && z.NameZone == name);

                if (zone == null)
                {
                    _log.LogWarning(
                        $"[Devil] zone '{name}' not found.");
                    continue;
                }

                if (!zone.CanSpawnBoss || zone.SnipeZone)
                {
                    _log.LogWarning(
                        $"[Devil] zone '{name}' can't host bosses.");
                    continue;
                }

                pool.Add(zone);
            }

            if (pool.Count == 0)
                return;

            var ordered = pool.OrderBy(_ => Rng.Next()).ToList();

            wave._possibleShuffledZones = ordered;

            _log.LogInfo(
                $"[Devil] autonomous zones: " +
                $"{string.Join(", ", ordered.Select(z => z.NameZone))}");
        }

        private static bool IsNight()
        {
            try
            {
                var gdt = Singleton<GameWorld>.Instance.GameDateTime;
                var t = Traverse.Create(gdt);

                DateTime start;

                if (t.Field("StatedGameDateTime").FieldExists())
                {
                    start = t.Field("StatedGameDateTime")
                        .GetValue<DateTime>();
                }
                else if (t.Property("StatedGameDateTime").PropertyExists())
                {
                    start = t.Property("StatedGameDateTime")
                        .GetValue<DateTime>();
                }
                else
                {
                    throw new MissingMemberException(
                        "StatedGameDateTime not found");
                }

                int hour = start.Hour;

                _log.LogInfo(
                    $"[Devil] game start hour = {hour}");

                return hour >= NightStartHour || hour < NightEndHour;
            }
            catch (Exception ex)
            {
                _log.LogWarning(
                    $"[Devil] can't read game time: {ex.Message}");

                return false;
            }
        }

        public static void ResetRaid()
        {
            _raidRoll = -1;
            _raidIsNight = false;
            _soloSelected = false;
            _supportSelected = false;
            _supportDecisionMade = false;
            _devilSpawned = false;
            _devilSpawnAttempted = false;

            CultistSpawn.ResetRaid();

            LoggedWaves.Clear();

            _log?.LogInfo(
                "[Devil] raid state reset.");
        }
    }
}