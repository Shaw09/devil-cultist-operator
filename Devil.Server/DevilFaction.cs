using MoreBotsServer.Services;
using SPTarkov.Server.Core.Models.Eft.Common;

namespace Devil.Server
{
    public static class DevilFaction
    {
        public static void Register(FactionService factionService)
        {
            if (factionService.Factions.TryGetValue("cultists", out var cultists))
            {
                cultists.BotTypes.Add((WildSpawnType)960234);
            }
        }
    }
}
