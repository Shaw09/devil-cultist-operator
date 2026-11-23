using MoreBotsServer.Services;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Spt.Tables;
using System;
using System.Collections.Generic;

namespace Devil.Server;

public static class DevilFaction
{
    public static void Register(
        FactionService factionService,
        BotTable botTable)
    {
        const string factionName = "cultists";

        var devilType = (WildSpawnType)ModInfo.DevilWildSpawnType;

        if (!factionService.Factions.TryGetValue(
                factionName, out var cultists))
        {
            DevilLogger.Error("Faction 'cultists' was not found!");
            return;
        }

        if (!cultists.BotTypes.Contains(devilType))
        {
            cultists.BotTypes.Add(devilType);
            DevilLogger.Info("Devil added to cultists faction.");
        }

        InitializeEnemyLists(cultists.BotTypes, botTable);
        factionService.AddFriendlyByFaction(
            factionName, factionName);

        int registeredFactions = 0;

        foreach (var faction in factionService.Factions)
        {
            if (faction.Key == factionName)
                continue;

            if (faction.Value.BotTypes == null ||
                faction.Value.BotTypes.Count == 0)
                continue;

            factionService.AddEnemyByFaction(
                factionName, faction.Key);

            factionService.AddEnemyByFaction(
                faction.Key, factionName);

            registeredFactions++;
        }

        RegisterDevilEnemies(factionService, botTable);

        DevilLogger.Info(
            $"Faction relations registered. Enemy factions: {registeredFactions}.");

    }

    private static void InitializeEnemyLists(
        IEnumerable<WildSpawnType> botTypes,
        BotTable botTable)
    {
        string[] difficulties =
        {
            "easy",
            "normal",
            "hard",
            "impossible"
        };

        foreach (var spawnType in botTypes)
        {
            string? botKey =
                (int)spawnType == ModInfo.DevilWildSpawnType
                    ? ModInfo.DevilBotKey.ToLowerInvariant()
                    : Enum.GetName(spawnType)?.ToLowerInvariant();

            if (string.IsNullOrEmpty(botKey) ||
                !botTable.Types.TryGetValue(botKey, out var bot) ||
                bot is null)
            {
                var enumName = Enum.GetName(spawnType);

                if (enumName is "peacefullZryachiyEvent"
                    or "sectactPriestEvent")
                    continue;

                DevilLogger.Warning(
                    $"BotType '{spawnType}' not found in BotTable.");

                continue;
            }

            var botDifficulties = bot.BotDifficulty;

            if (botDifficulties == null)
                continue;

            foreach (var difficultyName in difficulties)
            {
                if (!botDifficulties.TryGetValue(
                        difficultyName, out var config))
                    continue;

                var mind = config?.Mind;

                if (mind == null)
                    continue;

                mind.EnemyBotTypes ??=
                    new List<WildSpawnType>();
            }
        }
    }

    private static void RegisterDevilEnemies(
        FactionService factionService,
        BotTable botTable)
    {
        string devilKey = ModInfo.DevilBotKey.ToLowerInvariant();

        if (!botTable.Types.TryGetValue(
                devilKey, out var devilBot) ||
            devilBot is null)
        {
            DevilLogger.Error(
                $"Devil BotType '{devilKey}' not found.");

            return;
        }

        var enemyTypes = new HashSet<WildSpawnType>();

        foreach (var faction in factionService.Factions)
        {
            if (faction.Key == "cultists")
                continue;

            if (faction.Value.BotTypes == null ||
                faction.Value.BotTypes.Count == 0)
                continue;

            foreach (var enemyType in faction.Value.GetAllBotTypes())
            {
                enemyTypes.Add(enemyType);
            }
        }

        var botDifficulties = devilBot.BotDifficulty;

        if (botDifficulties == null)
        {
            DevilLogger.Warning(
                "Devil BotDifficulty is null.");

            return;
        }

        string[] difficulties =
        {
            "easy",
            "normal",
            "hard",
            "impossible"
        };

        int registeredProfiles = 0;

        foreach (var difficultyName in difficulties)
        {
            if (!botDifficulties.TryGetValue(
                    difficultyName, out var difficulty))
                continue;

            var mind = difficulty?.Mind;

            if (mind == null)
                continue;

            mind.EnemyBotTypes ??=
                new List<WildSpawnType>();

            foreach (var enemyType in enemyTypes)
            {
                if (!mind.EnemyBotTypes.Contains(enemyType))
                    mind.EnemyBotTypes.Add(enemyType);
            }

            registeredProfiles++;
        }

        DevilLogger.Info(
            $"Devil enemies registered: {enemyTypes.Count} types across {registeredProfiles} difficulty profiles.");
    }
}