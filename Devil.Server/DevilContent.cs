using Devil.Server.Loadouts;
using MoreBotsServer;
using MoreBotsServer.Services;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common;
using System.Reflection;

namespace Devil.Server;

[Injectable(InjectionType = InjectionType.Singleton, TypePriority = MoreBotsServer.MoreBotsLoadOrder.LoadBots)]
public sealed class DevilContent(
    MoreBotsServer.MoreBotsAPI moreBots,
    MoreBotsCustomBotTypeService customBotTypeService,
    FactionService factionService,
    MoreBotsLogger moreBotsLogger,
    ReserveAmmoPatch reserveAmmoPatch,
    MedicalLoadoutPatch medicalLoadoutPatch,
    WTTServerCommonLib.WTTServerCommonLib commonLib) : IOnLoad
{
    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        DevilLogger.Initialize(moreBotsLogger);
        DevilLogger.Info("Starting Devil server initialization.");

        var assembly = Assembly.GetExecutingAssembly();

        await commonLib.CustomItemServiceExtended.CreateCustomItems(assembly);
        await commonLib.CustomLocaleService.CreateCustomLocales(assembly);
        await commonLib.CustomWeaponPresetService.CreateCustomWeaponPresets(assembly);
        await moreBots.LoadBots(assembly);
        reserveAmmoPatch.Start();
        medicalLoadoutPatch.Start();

        customBotTypeService.AddCustomWildSpawnTypeNames(new Dictionary<int, string>
        {
            { ModInfo.DevilWildSpawnType, ModInfo.DevilBotKey }
        });

        var devilType = (WildSpawnType)ModInfo.DevilWildSpawnType;


        if (factionService.Factions.TryGetValue("cultists", out var cultists))
        {
            if (!cultists.BotTypes.Contains(devilType))
            {
                cultists.BotTypes.Add(devilType);
                DevilLogger.Info("Successfully added Devil to the cultists faction.");
            }
            else
            {
                DevilLogger.Warning("Devil is already registered in the cultists faction.");
            }
        }
        else
        {
            DevilLogger.Error("Faction 'cultists' was not found!");
        }

        DevilLogger.Info("Devil server initialization completed.");
    }
}
