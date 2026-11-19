using System;
using BepInEx.Logging;

namespace Devil.Client.Spawn
{
    internal static class CultistSpawn
    {
        private static readonly Random Rng = new();

        private static double _raidRoll = -1;
        private static bool _spawnSelected;

        private static ManualLogSource _log;

        public static void Initialize(ManualLogSource log)
        {
            _log = log;
        }

        public static bool ShouldSpawn()
        {
            if (_raidRoll >= 0)
                return _spawnSelected;

            double chance = Math.Clamp(
                Plugin.Config.CultistChance,
                0,
                100
            );

            _raidRoll = Rng.NextDouble() * 100.0;
            _spawnSelected = _raidRoll < chance;

            _log?.LogInfo(
                $"[CultistGang] Raid roll={_raidRoll:0.0}, " +
                $"chance={chance}, selected={_spawnSelected}"
            );

            return _spawnSelected;
        }

        public static void ResetRaid()
        {
            _raidRoll = -1;
            _spawnSelected = false;

            _log?.LogInfo("[CultistGang] Raid state reset.");
        }
    }
}