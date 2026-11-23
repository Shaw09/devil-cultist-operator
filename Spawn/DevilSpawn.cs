using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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

        private const WildSpawnType DevilWildSpawnType =
            (WildSpawnType)Plugin.DevilWildSpawnTypeId;

        private const int NightStartHour = 22;
        private const int NightEndHour = 7;

        private static readonly System.Random Rng = new();

        private static ManualLogSource _log;
        private static MethodInfo _spawnMethod;

        private static double _raidRoll = -1;
        private static bool _raidIsNight;
        private static bool _soloSelected;
        private static BossLocationSpawn _devilWaveInstance;
        private static bool _harbingerWaveSeen;
        private static BotZone _harbingerZone;
        private static GameWorld _raidWorld;
        private static BossLocationSpawn _harbingerWaveInstance;


        public static bool Install(Harmony harmony, ManualLogSource log)
        {
            _log = log;

            _spawnMethod = AccessTools.GetDeclaredMethods(typeof(BotBossSpawn))
                .FirstOrDefault(m =>
                    m.Name == "Spawn" &&
                    m.GetParameters().Length == 3 &&
                    m.GetParameters()[0].ParameterType ==
                    typeof(BossLocationSpawn));

            if (_spawnMethod == null)
            {
                log.LogError("[Devil] BotBossSpawn.Spawn not found.");
                return false;
            }

            harmony.Patch(_spawnMethod, prefix: new HarmonyMethod(typeof(DevilSpawn), nameof(SpawnPrefix))
            {
                priority = Priority.First
            });

            harmony.Patch(_spawnMethod, postfix: new HarmonyMethod(typeof(DevilSpawn), nameof(SpawnPostfix)));

            log.LogInfo("[Devil] Per-raid spawn controller installed.");

            return true;
        }

        private static string FormatRoll(double roll)
        {
            return roll < 0
                ? "NOT_EVALUATED"
                : roll.ToString("0.0");
        }

        private static bool Is(BossLocationSpawn wave, string name)
        {
            return wave != null &&
                string.Equals(
                    wave.BossName,
                    name,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool EnsureRaidDecision()
        {
            if (_raidRoll >= 0)
                return _soloSelected;

            var settings = Plugin.ServerSettings;

            if (settings == null)
            {
                _log?.LogWarning(
                    "[Devil][Decision] Server settings unavailable. Spawn blocked.");

                _soloSelected = false;
                return false;
            }

            double baseChance = Math.Clamp(
                settings.SpawnChanceDevil, 0, 100);

            double harbingerBonus = _harbingerWaveSeen
                ? Math.Clamp(settings.DevilHarbingerBonusChance, 0, 100)
                : 0;

            double effectiveChance = Math.Clamp(
                baseChance + harbingerBonus, 0, 100);

            _raidIsNight = IsNight();
            _raidRoll = Rng.NextDouble() * 100.0;

            _soloSelected =
                _raidIsNight && _raidRoll < effectiveChance;

            _log?.LogInfo(
                "[Devil][Decision] CREATED: " +
                $"Night={_raidIsNight}, " +
                $"HarbingerSeen={_harbingerWaveSeen}, " +
                $"BaseChance={baseChance}%, " +
                $"HarbingerBonus={harbingerBonus}%, " +
                $"EffectiveChance={effectiveChance}%, " +
                $"Roll={_raidRoll:0.0}, " +
                $"Selected={_soloSelected}");

            return _soloSelected;
        }

        private static bool SpawnPrefix(BotBossSpawn __instance, BossLocationSpawn wave, object[] __args, ref Task __result)
        {
            try
            {
                if (wave == null)
                    return true;

                var world = Singleton<GameWorld>.Instance;

                if (!ReferenceEquals(world, _raidWorld))
                {
                    _log?.LogInfo("[Devil][Reset] New GameWorld detected in SpawnPrefix.");
                    ResetRaid();
                    _raidWorld = world;
                }

                bool devilWave = Is(wave, BossName);
                bool harbingerWave = Is(wave, HarbingerName);

                if (!devilWave && !harbingerWave)
                    return true;

                if (harbingerWave)
                {
                    _harbingerWaveSeen = true;

                    if (!ReferenceEquals(_harbingerWaveInstance, wave))
                    {
                        _harbingerWaveInstance = wave;

                        ApplyOwnZones(
                            __instance,
                            wave,
                            isHarbinger: true);
                    }

                    _log?.LogInfo(
                        "[Harbinger] Wave detected. " +
                        $"Zone={_harbingerZone?.NameZone ?? "NONE"}");

                    return true;
                }

                if (devilWave)
                {
                    if (!EnsureRaidDecision())
                    {
                        __result = Task.CompletedTask;

                        _log?.LogInfo(
                            "[Devil][Autonomous] Spawn blocked by raid decision.");

                        return false;
                    }

                    if (_devilWaveInstance != null &&
                        !ReferenceEquals(_devilWaveInstance, wave))
                    {
                        __result = Task.CompletedTask;

                        _log?.LogWarning(
                            "[Devil][Autonomous] Different Devil wave instance detected. " +
                            "Blocking duplicate wave.");

                        return false;
                    }

                    bool isRetry = ReferenceEquals(_devilWaveInstance, wave);

                    if (!isRetry)
                    {
                        _devilWaveInstance = wave;

                        _log?.LogInfo(
                            "[Devil][Autonomous] First spawn attempt selected.");
                    }
                    else
                    {
                        _log?.LogInfo(
                            "[Devil][Autonomous] Retrying delayed spawn.");
                    }

                    ApplyOwnZones(__instance, wave);

                    _log?.LogInfo(
                        "[Devil][Autonomous] Allowing spawn. " +
                        $"HarbingerZone={_harbingerZone?.NameZone ?? "NONE"}");

                    return true;
                }
            }
            catch (Exception ex)
            {
                _log?.LogWarning($"[Devil] SpawnPrefix error: {ex}");

                return true;
            }

            return true;
        }

        private static void SpawnPostfix(BossLocationSpawn wave, Task __result)
        {
            if (wave == null || __result == null)
                return;

            if (!Is(wave, BossName) && !Is(wave, HarbingerName))
                return;

            __result.ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    _log?.LogError(
                        $"[Devil][Spawn] {wave.BossName} task failed: " +
                        $"{task.Exception?.GetBaseException().Message}");
                }
                else if (task.IsCanceled)
                {
                    _log?.LogWarning(
                        $"[Devil][Spawn] {wave.BossName} task was cancelled.");
                }
            });
        }

        private static void ApplyOwnZones(
            BotBossSpawn spawner,
            BossLocationSpawn wave,
            bool isHarbinger = false)
        {
            if (spawner?._allZones == null)
            {
                _log?.LogWarning("[Devil][Zones] Spawner or _allZones is null.");
                return;
            }

            // Devil riutilizza la zona effettivamente selezionata per Harbinger.
            if (!isHarbinger && _harbingerZone != null)
            {
                wave._possibleShuffledZones = new List<BotZone>
                {
                    _harbingerZone
                };

                _log?.LogInfo(
                    $"[Devil][Zones] Reusing Harbinger zone: " +
                    $"{_harbingerZone.NameZone}");

                return;
            }

            if (string.IsNullOrEmpty(wave?.BossZone))
            {
                _log?.LogWarning("[Devil][Zones] BossZone is empty.");
                return;
            }

            var pool = new List<BotZone>();

            foreach (var raw in wave.BossZone.Split(','))
            {
                var name = raw.Trim();

                if (name.Length == 0)
                    continue;

                var zone = spawner._allZones.FirstOrDefault(z =>
                    z != null && z.NameZone == name);

                if (zone == null)
                    continue;

                if (!zone.CanSpawnBoss || zone.SnipeZone)
                    continue;

                if (!pool.Contains(zone))
                    pool.Add(zone);
            }

            if (pool.Count == 0)
            {
                _log?.LogWarning(
                    "[Devil][Zones] No valid zones found. " +
                    "Original selection will remain unchanged.");

                return;
            }

            var selectedZone = pool[Rng.Next(pool.Count)];

            if (isHarbinger)
            {
                _harbingerZone = selectedZone;

                _log?.LogInfo(
                    $"[Devil][Zones] Harbinger zone selected: " +
                    $"{selectedZone.NameZone}");
            }
            else
            {
                _log?.LogInfo(
                    $"[Devil][Zones] Devil selected its own zone: " +
                    $"{selectedZone.NameZone}");
            }

            wave._possibleShuffledZones = new List<BotZone>
            {
                selectedZone
            };
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

                bool isNight =
                    hour >= NightStartHour ||
                    hour < NightEndHour;

                _log?.LogInfo(
                    $"[Devil][Time] Raid start hour={hour}, " +
                    $"Night={isNight}");

                return isNight;
            }
            catch (Exception ex)
            {
                _log?.LogWarning(
                    $"[Devil][Time] Can't read game time: {ex.Message}");

                return false;
            }
        }

        public static void ResetRaid()
        {
            _log?.LogInfo("[Devil][Reset] Resetting raid state.");

            _raidRoll = -1;
            _raidIsNight = false;
            _soloSelected = false;

            _harbingerWaveSeen = false;
            _harbingerZone = null;
            _harbingerWaveInstance = null;

            _devilWaveInstance = null;
            _raidWorld = null;

            _log?.LogInfo("[Devil][Reset] Reset completed.");
        }
    }
}