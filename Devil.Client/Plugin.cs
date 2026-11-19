using BepInEx;
using BepInEx.Logging;
using Comfort.Common;
using Devil.Client.Config;
using Devil.Client.Spawn;
using Devil.Client.Spawn.Integrations;
using EFT;
using HarmonyLib;

namespace Devil.Client
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.morebotsapi.tacticaltoaster")]
    [BepInDependency("xyz.drakia.bigbrain", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("me.sol.sain", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        internal const int DevilWildSpawnTypeId = 960234;
        internal const WildSpawnType DevilRole = (WildSpawnType)DevilWildSpawnTypeId;
        public const string PluginGuid = "spt.shaw.devil.client";
        public const string PluginName = "Devil Cultist Operator";
        public const string PluginVersion = "1.1.0";

        public static ManualLogSource Log { get; private set; }

        public static DevilConfig Config { get; private set; }

        private Harmony _harmony;
        private GameWorld _lastWorld;

        private void Awake()
        {
            Log = Logger;

            Config = DevilConfig.Load(Logger);

            _harmony = new Harmony(PluginGuid);

            CultistSpawn.Initialize(Logger);

            DevilSpawn.Install(_harmony, Logger);

            BossNotifierIntegration.Register(Logger);

            Logger.LogInfo($"{PluginName} loaded successfully!");
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

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}