using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;

namespace Devil.Client.Spawn.Integrations
{
    internal static class BossNotifierIntegration
    {
        private const string BossNotifierGUID = "Mattexe.BossNotifier";

        public static void Register(ManualLogSource log)
        {
            try
            {
                if (!BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(BossNotifierGUID, out var pluginInfo))
                {
                    log.LogInfo("[Devil] BossNotifier not installed. Skipping integration.");
                    return;
                }

                var assembly = pluginInfo.Instance.GetType().Assembly;
                var type = assembly.GetType("BossNotifier.BossNotifierPlugin");

                if (type == null)
                {
                    log.LogWarning("[Devil] BossNotifierPlugin type not found.");
                    return;
                }

                AddDictionaryEntry(type, "customBossLookup", Plugin.DevilWildSpawnTypeId, "Devil");

                AddDictionaryEntry(type, "customBossDisplayNames", Plugin.DevilWildSpawnTypeId, "Devil");

                AddHashSetEntry(type, "customBossGroups", "Devil");

                RegisterMarkerConfig(pluginInfo.Instance, log);

                log.LogInfo("[Devil] BossNotifier integration registered.");
            }
            catch (Exception ex)
            {
                log.LogError($"[Devil] BossNotifier integration failed: {ex}");
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

        private static void RegisterMarkerConfig(BaseUnityPlugin bossNotifier, ManualLogSource log)
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
                log.LogWarning("[Devil] BossNotifier marker dictionaries not found.");
                return;
            }

            var markerCharacter = bossNotifier.Config.Bind(
                "Custom Boss Markers",
                "Devil Marker",
                "\u2605",
                "Marker character for Devil.");

            var markerColor = bossNotifier.Config.Bind(
                "Custom Boss Markers",
                "Devil Color",
                new UnityEngine.Color(0.25f, 0.05f, 0.35f, 1f),
                "Marker color for Devil.");

            characters["Devil"] = markerCharacter;
            colors["Devil"] = markerColor;
        }
    }
}