using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace Devil.Client.Spawn.Integrations
{
    internal static class BossNotifierIntegration
    {
        private const string BossNotifierGUID = "Mattexe.BossNotifier";

        private class Member
        {
            public int Id;
            public string DisplayName;

            public Member(int id, string displayName)
            {
                Id = id;
                DisplayName = displayName;
            }
        }

        public static void Register(ManualLogSource log)
        {
            try
            {
                if (!BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(BossNotifierGUID, out var pluginInfo) || pluginInfo.Instance == null)
                {
                    log.LogInfo("[Devil] BossNotifier not installed. Skipping integration.");
                    return;
                }

                var plugin = pluginInfo.Instance;
                var type = plugin.GetType();

                RegisterGroup(
                    plugin, type, log,
                    groupName: "Devil",
                    isPlural: false,
                    members: new[]
                    {
                        new Member(Plugin.DevilWildSpawnTypeId, "Devil"),
                    },
                    defaultChar: "\u2605",
                    defaultColor: new Color(0.25f, 0.05f, 0.35f, 1f));

                RegisterGroup(
                    plugin, type, log,
                    groupName: "CultGang",
                    isPlural: true,
                    members: new[]
                    {
                        new Member(57, "Predvestnik"),
                        new Member(58, "Prizrak"),
                        new Member(59, "Oni"),
                    },
                    defaultChar: "\u2620",
                    defaultColor: new Color(0.6f, 0.1f, 0.1f, 1f));

                log.LogInfo("[Devil] BossNotifier integration registered (Devil + CultGang).");
            }
            catch (Exception ex)
            {
                log.LogError($"[Devil] BossNotifier integration failed: {ex}");
            }
        }

        private static void RegisterGroup(
            BaseUnityPlugin plugin, Type type, ManualLogSource log,
            string groupName, bool isPlural, Member[] members,
            string defaultChar, Color defaultColor)
        {
            var lookup = GetStatic(type, "customBossLookup") as IDictionary;
            var displayNames = GetStatic(type, "customBossDisplayNames") as IDictionary;
            var groups = GetStatic(type, "customBossGroups") as ICollection<string>;
            var plural = GetStatic(type, "pluralBosses") as ICollection<string>;
            var characters = GetStatic(type, "customGroupMarkerCharacters") as IDictionary;
            var colors = GetStatic(type, "customGroupMarkerColors") as IDictionary;

            if (lookup == null || displayNames == null || groups == null)
            {
                log.LogWarning($"[Devil] BossNotifier dictionaries not found for {groupName}.");
                return;
            }

            foreach (var m in members)
            {
                if (m.Id == 0) continue;
                lookup[m.Id] = groupName;
                displayNames[m.Id] = m.DisplayName;
            }

            if (!groups.Contains(groupName)) groups.Add(groupName);

            if (isPlural && plural != null && !plural.Contains(groupName)) plural.Add(groupName);

            if (characters != null && colors != null)
            {
                const string section = "Custom Boss Markers";

                characters[groupName] = plugin.Config.Bind(
                    section, $"{groupName} Marker", defaultChar,
                    $"Marker character for {groupName}.");

                colors[groupName] = plugin.Config.Bind(
                    section, $"{groupName} Color", defaultColor,
                    $"Marker color for {groupName}.");
            }
            else
            {
                log.LogWarning($"[Devil] BossNotifier marker dictionaries not found for {groupName}.");
            }
        }

        private static object GetStatic(Type type, string name) =>
            type.GetField(name, BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
    }
}