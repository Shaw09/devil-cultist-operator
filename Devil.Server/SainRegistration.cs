using MoreBotsServer.Interop;
using MoreBotsServer.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;

namespace Devil.Server;

[Injectable(TypePriority = OnLoadOrder.Preload + 2)]
public sealed class SainRegistration(SainInteropRegistration sainInterop) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        sainInterop.RegisterBotType(new MoreBotsSainBotTypeRegistration
        {
            WildSpawnType = ModInfo.DevilWildSpawnType,
            BotDbKey = ModInfo.DevilBotKey,
            Name = "Devil",
            Section = "Cultist Operator",
            Description = "Former Navy SEAL turned cult operator. Silent, patient and lethal: advances tactically, " +
              "and opens engagements with stun, smoke and fragmentation grenades.",
            DifficultyModifier = 0.95f,
            BrainsToApply = ["SectantWarrior", "PMC", "ExUsec"],
            LayersToRemove = ["Request", "KnightFight", "PmcBear", "PmcUsec", "ExURequest", "StationaryWS"]
        });

        return Task.CompletedTask;
    }
}