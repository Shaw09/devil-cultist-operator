using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using HarmonyLib;
using System.Collections;
using UnityEngine;

namespace Devil.Client
{
    [BepInDependency("com.morebotsapi.tacticaltoaster")]
    [BepInDependency("xyz.drakia.bigbrain", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("me.sol.sain", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Mattexe.BossNotifier", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.mpstark.dynamicmaps", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin("spt.shaw.devil.client", "Devil Client", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private const int DevilWildSpawnType = 960234;
        private const string BossNotifierGUID = "Mattexe.BossNotifier";
        private GameWorld _lastWorld;
        private static ManualLogSource _logger;

        private void Awake()
        {
            _logger = Logger;

            var harmony = new Harmony("spt.shaw.devil.client");

            if (!DevilSpawn.Install(harmony, Logger))
                Logger.LogError("[Devil] spawn control not installed.");

            RegisterBossNotifier();
            RegisterDynamicMaps(harmony);
        }

        private void RegisterBossNotifier()
        {
            try
            {
                if (!BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(BossNotifierGUID, out var pluginInfo))
                {
                    Logger.LogInfo("[Devil] BossNotifier not installed. Skipping integration.");
                    return;
                }

                var assembly = pluginInfo.Instance.GetType().Assembly;
                var type = assembly.GetType("BossNotifier.BossNotifierPlugin");

                if (type == null)
                {
                    Logger.LogWarning("[Devil] BossNotifierPlugin type not found.");
                    return;
                }

                AddDictionaryEntry(type, "customBossLookup", DevilWildSpawnType, "Devil");

                AddDictionaryEntry(type, "customBossDisplayNames", DevilWildSpawnType, "Devil");

                AddHashSetEntry(type, "customBossGroups", "Devil");

                RegisterMarkerConfig(pluginInfo.Instance);

                Logger.LogInfo("[Devil] BossNotifier integration registered.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[Devil] BossNotifier integration failed: {ex}");
            }
        }

        private static void AddDictionaryEntry(Type type, string fieldName, int id, string value)
        {
            var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);

            if (field?.GetValue(null) is System.Collections.IDictionary dictionary)
                dictionary[id] = value;
        }

        private static void AddHashSetEntry(Type type, string fieldName, string value)
        {
            var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);

            if (field?.GetValue(null) is System.Collections.IList)
                return;

            var collection = field?.GetValue(null);

            collection?.GetType().GetMethod("Add")?.Invoke(
                collection,
                new object[] { value });
        }

        private void RegisterMarkerConfig(BaseUnityPlugin bossNotifier)
        {
            var type = bossNotifier.GetType();

            var charactersField = type.GetField(
                "customGroupMarkerCharacters",
                BindingFlags.Public | BindingFlags.Static);

            var colorsField = type.GetField(
                "customGroupMarkerColors",
                BindingFlags.Public | BindingFlags.Static);

            if (charactersField?.GetValue(null) is not System.Collections.IDictionary characters ||
                colorsField?.GetValue(null) is not System.Collections.IDictionary colors)
            {
                Logger.LogWarning("[Devil] BossNotifier marker dictionaries not found.");
                return;
            }

            var markerCharacter = bossNotifier.Config.Bind(
                "Custom Boss Markers",
                "Devil Marker",
                "★",
                "Marker character for Devil.");

            var markerColor = bossNotifier.Config.Bind(
                "Custom Boss Markers",
                "Devil Color",
                new UnityEngine.Color(0.25f, 0.05f, 0.35f, 1f),
                "Marker color for Devil.");

            characters["Devil"] = markerCharacter;
            colors["Devil"] = markerColor;
        }

        private void RegisterDynamicMaps(Harmony harmony)
        {
            try
            {
                var gameUtils = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("DynamicMaps.Utils.GameUtils"))
                    .FirstOrDefault(t => t != null);

                if (gameUtils == null)
                {
                    Logger.LogInfo("[Devil] DynamicMaps not installed. Skipping integration.");
                    return;
                }

                var field = gameUtils.GetField("_trackedBosses", BindingFlags.NonPublic | BindingFlags.Static);

                var collection = field?.GetValue(null);

                if (collection == null)
                {
                    Logger.LogWarning("[Devil] DynamicMaps boss collection not found.");
                    return;
                }

                var contains = collection.GetType().GetMethod("Contains", new[] { typeof(WildSpawnType) });
                var add = collection.GetType().GetMethod("Add", new[] { typeof(WildSpawnType) });

                var devilType = (WildSpawnType)960234;

                if (contains == null || add == null)
                {
                    Logger.LogWarning("[Devil] Cannot access DynamicMaps boss collection.");
                    return;
                }

                bool exists = (bool)contains.Invoke(collection, new object[] { devilType });

                if (!exists)
                {
                    add.Invoke(collection, new object[] { devilType });
                }

                var markerType = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .Select(a => a.GetType("DynamicMaps.UI.Components.PlayerMapMarker"))
                    .FirstOrDefault(t => t != null);

                var createMethod = markerType?.GetMethod(
                    "Create",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[]
                    {
                        typeof(IPlayer),
                        typeof(GameObject),
                        typeof(string),
                        typeof(Color),
                        typeof(string),
                        typeof(Vector2),
                        typeof(float),
                        typeof(float)
                    },
                    null);

                if (createMethod == null)
                {
                    Logger.LogWarning("[Devil] PlayerMapMarker.Create not found.");
                    return;
                }

                harmony.Patch(
                    createMethod,
                    postfix: new HarmonyMethod(
                        typeof(Plugin),
                        nameof(PlayerMarkerPostfix)));

                Logger.LogInfo("[Devil] Registered as a DynamicMaps boss.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[Devil] DynamicMaps integration failed: {ex}");
            }
        }

        private static void PlayerMarkerPostfix(IPlayer player, object __result)
        {
            try
            {
                if (player?.Profile?.Info?.Settings == null || __result == null)
                    return;

                if ((int)player.Profile.Info.Settings.Role != DevilWildSpawnType)
                    return;

                var colorProperty = __result.GetType().GetProperty(
                    "Color",
                    BindingFlags.Public | BindingFlags.Instance);

                if (colorProperty == null || !colorProperty.CanWrite)
                {
                    _logger?.LogWarning("[Devil] DynamicMaps marker Color property not found.");
                    return;
                }

                colorProperty.SetValue(
                    __result,
                    new Color32(64, 13, 89, 255));
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"[Devil] DynamicMaps marker color error: {ex}");
            }
        }

        private void Update()
        {
            var currentWorld = Singleton<GameWorld>.Instance;

            if (currentWorld == null)
            {
                if (_lastWorld != null)
                {
                    DevilSpawn.ResetRaid();
                    _lastWorld = null;
                }

                return;
            }

            if (_lastWorld != null &&
                !ReferenceEquals(_lastWorld, currentWorld))
            {
                DevilSpawn.ResetRaid();
            }

            _lastWorld = currentWorld;
        }
    }

    internal static class DevilSpawn
    {
        public const string BossName = "bossDevil";
        private const string HarbingerName = "sectantPredvestnik";
        private const WildSpawnType DevilWildSpawnType = (WildSpawnType)960234;

        private const double NightChance = 20;
        private const double CultistChance = 20;

        private const int NightStartHour = 22;
        private const int NightEndHour = 7;

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
                _raidIsNight && _raidRoll < NightChance;

            _supportSelected =
                _raidRoll >= NightChance &&
                _raidRoll < NightChance + CultistChance;

            _log.LogInfo(
                $"[Devil] Raid roll={_raidRoll:0.0}, " +
                $"night={_raidIsNight}, " +
                $"solo={_soloSelected}, " +
                $"support={_supportSelected}");
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
                    var supports = wave.Supports;

                    if (supports == null)
                    {
                        _log.LogWarning(
                            "[Devil] Harbinger supports are null.");

                        return true;
                    }

                    if (!_supportSelected || _devilSpawnAttempted)
                    {
                        int removed = supports.Count(s =>
                            s.BossEscortType == DevilWildSpawnType);

                        wave.Supports = supports
                            .Where(s => s.BossEscortType != DevilWildSpawnType)
                            .ToArray();

                        _log.LogInfo(
                            $"[Devil] support removed. " +
                            $"Removed={removed}, " +
                            $"SupportSelected={_supportSelected}, " +
                            $"AlreadyAttempted={_devilSpawnAttempted}");
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
            _devilSpawned = false;
            _devilSpawnAttempted = false;

            LoggedWaves.Clear();

            _log?.LogInfo(
                "[Devil] raid state reset.");
        }
    }
}