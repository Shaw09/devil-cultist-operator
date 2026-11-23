using System.Collections.Generic;
using Mono.Cecil;
using MoreBotsAPI;

namespace Devil.Prepatch;

public static class BotTypeRegistrationPatch
{
    private const int SectantWarriorBrain = 20;
    private const int DevilId = 960234;

    private static readonly List<int> ImpossibleDifficultyOnly =
    [
        0,
        2,
        3
    ];

    public static IEnumerable<string> TargetDLLs { get; } =
    [
        "Assembly-CSharp.dll"
    ];

    public static void Patch(ref AssemblyDefinition assembly)
    {
        var devil = new CustomWildSpawnType(
            DevilId,
            "bossDevil",
            "Boss",
            SectantWarriorBrain,
            isBoss: true,
            isFollower: false,
            isHostileToEverybody: false);

        devil.SetCountAsBossForStatistics(true);
        devil.SetShouldUseFenceNoBossAttack(false, false);
        devil.SetExcludedDifficulties(ImpossibleDifficultyOnly);
        CustomWildSpawnTypeManager.RegisterWildSpawnType(devil, assembly);
    }
}
