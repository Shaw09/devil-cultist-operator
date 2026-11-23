using Devil.Server.Loadouts;
using MoreBotsServer;
using MoreBotsServer.Services;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Tables;
using System.Reflection;

namespace Devil.Server;

[Injectable(
    InjectionType = InjectionType.Singleton,
    TypePriority = MoreBotsServer.MoreBotsLoadOrder.LoadBots)]
public sealed class DevilContent(
    MoreBotsAPI moreBots,
    MoreBotsCustomBotTypeService customBotTypeService,
    FactionService factionService,
    BotTable botTable,
    MoreBotsLogger moreBotsLogger,
    ReserveAmmoPatch reserveAmmoPatch,
    MedicalLoadoutPatch medicalLoadoutPatch) : IOnLoad
{
    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        DevilLogger.Initialize(moreBotsLogger);

        var assembly = Assembly.GetExecutingAssembly();

        await moreBots.LoadBots(assembly);

        reserveAmmoPatch.Start();
        medicalLoadoutPatch.Start();
        customBotTypeService.AddCustomWildSpawnTypeNames(
            new Dictionary<int, string>
            {
                { ModInfo.DevilWildSpawnType, ModInfo.DevilBotKey }
            });

        DevilFaction.Register(factionService, botTable);
    }
}
