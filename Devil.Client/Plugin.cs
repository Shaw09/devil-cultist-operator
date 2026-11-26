using System;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Logging;
using Comfort.Common;
using Devil.Client.Config;
using Devil.Client.Spawn;
using Devil.Client.Spawn.Integrations;
using EFT;
using HarmonyLib;
using Newtonsoft.Json;
using SPT.Common.Http;
using UnityEngine;

namespace Devil.Client
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.morebotsapi.tacticaltoaster")]
    [BepInDependency("xyz.drakia.bigbrain", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("me.sol.sain", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.mpstark.dynamicmaps", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        internal const int DevilWildSpawnTypeId = 960234;
        internal const WildSpawnType DevilRole =
            (WildSpawnType)DevilWildSpawnTypeId;

        public const string PluginGuid = "spt.shaw.devil.client";
        public const string PluginName = "Devil Cultist Operator";
        public const string PluginVersion = "2.0.1";

        public static ManualLogSource Log { get; private set; }

        public static DevilServerSettings ServerSettings { get; private set; }

        private bool _settingsLoading;
        private float _nextSettingsRefresh;

        private Harmony _harmony;
        private GameWorld _lastWorld;

        private void Awake()
        {
            Log = Logger;

            _harmony = new Harmony(PluginGuid);

            DevilSpawn.Install(_harmony, Logger);

            BossNotifierIntegration.Register(Logger);
            DynamicMapsColorIntegration.Initialize(_harmony, Logger);

            _ = ReloadServerSettingsAsync();

            Logger.LogInfo($"{PluginName} loaded successfully!");
        }

        private async Task ReloadServerSettingsAsync()
        {
            if (_settingsLoading)
                return;

            _settingsLoading = true;

            try
            {
                string json = await RequestHandler.GetJsonAsync(
                    "/devil-cultist-operator/config");

                var settings =
                    JsonConvert.DeserializeObject<DevilServerSettings>(json);

                if (settings == null)
                {
                    throw new InvalidOperationException(
                        "Server returned an empty configuration.");
                }

                settings.Normalize();

                ServerSettings = settings;

                Logger.LogInfo(
                    "[Devil][ServerConfig] Loaded: " +
                    $"SpawnChanceDevil={settings.SpawnChanceDevil}%, " +
                    $"DevilHarbingerBonusChance={settings.DevilHarbingerBonusChance}%");
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    $"[Devil][ServerConfig] Failed to load settings: {ex.Message}");
            }
            finally
            {
                _settingsLoading = false;
                _nextSettingsRefresh = Time.unscaledTime + 15f;
            }
        }

        private void Update()
        {
            if (!_settingsLoading &&
                Time.unscaledTime >= _nextSettingsRefresh)
            {
                _ = ReloadServerSettingsAsync();
            }

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

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}