using System.Reflection;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Services.InRaid;
using Devil.Server.Controller;

namespace Devil.Server;

/// <summary>
/// Compatibility with spawn-overhaul mods such as acidphantasm-botplacementsystem (ABPS).
///
/// DevilWaves / CultistWaves add their waves to the map tables at server start. ABPS (MapSpawns) runs later
/// (OnLoadOrder.PostLoad + 69420) and replaces every map's BossLocationSpawn with its own list, and does it again
/// after every raid (RaidFinished -> RebuildMap). The Devil and Cult Gang waves therefore never reach the client.
///
/// This postfix runs on the per-raid copy of the map that is sent to the client (after ABPS and the vanilla raid-time
/// adjustments) and re-adds our waves only when they are missing. Without ABPS the waves are already there and
/// nothing changes.
/// </summary>
[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.PostLoad + 101)]
public sealed class RaidWaveGuardPatch : AbstractPatch, IOnLoad
{
    private const string Log = "[Devil/RaidWaves]";

    private static DevilWaves? _devilWaves;
    private static CultistWaves? _cultistWaves;
    private static ISptLogger<RaidWaveGuardPatch>? _logger;

    public RaidWaveGuardPatch(
        DevilWaves devilWaves,
        CultistWaves cultistWaves,
        ISptLogger<RaidWaveGuardPatch> logger) : base(nameof(RaidWaveGuardPatch))
    {
        _devilWaves = devilWaves;
        _cultistWaves = cultistWaves;
        _logger = logger;
    }

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        // Enable() only works when called from this assembly (AbstractPatch ownership check),
        // so other mods' patch managers cannot enable it twice.
        Enable();
        return Task.CompletedTask;
    }

    protected override MethodBase? GetTargetMethod()
    {
        return typeof(LocationLifecycleService).GetMethod(
            nameof(LocationLifecycleService.GenerateLocationAndLoot),
            BindingFlags.Instance | BindingFlags.Public);
    }

    [PatchPostfix]
    private static void Postfix(string name, LocationBase __result)
    {
        try
        {
            var spawns = __result?.BossLocationSpawn;
            if (spawns == null || string.IsNullOrEmpty(name) || _devilWaves == null || _cultistWaves == null)
            {
                return;
            }

            var map = name.ToLowerInvariant();

            lock (DevilServerConfigService.WaveLock)
            {
                if (!spawns.Any(DevilWaves.IsDevilWave))
                {
                    var devil = _devilWaves.CreateWaveForMap(map, __result!);
                    if (devil != null)
                    {
                        spawns.Add(devil);
                        _logger?.Info($"{Log} '{map}': Devil wave was missing (removed by another spawn mod), re-added. Escort={devil.BossEscortAmount}");
                    }
                }

                if (!spawns.Any(CultistWaves.IsCultWave))
                {
                    var cult = _cultistWaves.CreateWaveForMap(map);
                    if (cult != null)
                    {
                        spawns.Add(cult);
                        _logger?.Info($"{Log} '{map}': Cult Gang wave was missing (removed by another spawn mod), re-added. Chance={cult.BossChance}%, Escort={cult.BossEscortAmount}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.Warning($"{Log} could not restore waves for '{name}': {ex.Message}");
        }
    }
}
