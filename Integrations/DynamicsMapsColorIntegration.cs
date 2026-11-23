using System;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Devil.Client.Spawn.Integrations
{
    internal static class DynamicMapsColorIntegration
    {
        private const int DevilWildSpawnType = 960234;

        private static readonly Color DevilColor =
            new Color32(59, 10, 94, 255);

        private static readonly Color CultistColor =
            new Color32(59, 10, 94, 255);

        private static readonly Color LightPurpleColor =
            new Color32(105, 65, 135, 255);

        private static ManualLogSource _log;
        private static bool _patched;
        private static bool _errorLogged;

        public static void Initialize(
            Harmony harmony,
            ManualLogSource log)
        {
            if (_patched)
                return;

            _log = log;

            var mapViewType = AccessTools.TypeByName(
                "DynamicMaps.UI.Components.MapView");

            if (mapViewType == null)
            {
                _log.LogInfo(
                    "[DynamicMaps] Not installed. Color integration skipped.");
                return;
            }

            var target = AccessTools.Method(
                mapViewType,
                "AddPlayerMarker");

            if (target == null)
            {
                _log.LogWarning(
                    "[DynamicMaps] AddPlayerMarker not found.");
                return;
            }

            var postfix = AccessTools.Method(
                typeof(DynamicMapsColorIntegration),
                nameof(AddPlayerMarkerPostfix));

            harmony.Patch(
                target,
                postfix: new HarmonyMethod(postfix));

            _patched = true;

            _log.LogInfo(
                "[DynamicMaps] Custom marker colors enabled.");
        }

        private static void AddPlayerMarkerPostfix(
            object __0,
            object __result)
        {
            if (__0 == null || __result == null)
                return;

            try
            {
                object profile = GetMember(__0, "Profile");
                object info = GetMember(profile, "Info");
                object settings = GetMember(info, "Settings");
                object role = GetMember(settings, "Role");

                if (role == null)
                    return;

                int roleId = Convert.ToInt32(role);
                string roleName = role.ToString();

                Color? selectedColor = null;

                if (roleId == DevilWildSpawnType)
                {
                    selectedColor = DevilColor;
                }
                else if (
                    string.Equals(roleName, "sectantPriest",
                        StringComparison.OrdinalIgnoreCase) ||

                    string.Equals(roleName, "sectantWarrior",
                        StringComparison.OrdinalIgnoreCase) ||

                    string.Equals(roleName, "bossZryachiy",
                        StringComparison.OrdinalIgnoreCase) ||

                    string.Equals(roleName, "followerZryachiy",
                        StringComparison.OrdinalIgnoreCase))
                {
                    selectedColor = LightPurpleColor;
                }
                else if (roleName.StartsWith(
                    "sectant",
                    StringComparison.OrdinalIgnoreCase))
                {
                    selectedColor = CultistColor;
                }

                if (!selectedColor.HasValue)
                    return;

                var colorProperty = AccessTools.Property(
                    __result.GetType(),
                    "Color");

                colorProperty?.SetValue(
                    __result,
                    selectedColor.Value,
                    null);
            }
            catch (Exception ex)
            {
                if (!_errorLogged)
                {
                    _errorLogged = true;

                    _log.LogWarning(
                        $"[DynamicMaps] Marker color error: {ex}");
                }
            }
        }

        private static object GetMember(
            object instance,
            string name)
        {
            if (instance == null)
                return null;

            var type = instance.GetType();

            var field = AccessTools.Field(type, name);

            if (field != null)
                return field.GetValue(instance);

            var property = AccessTools.Property(type, name);

            if (property != null)
                return property.GetValue(instance, null);

            return null;
        }
    }
}