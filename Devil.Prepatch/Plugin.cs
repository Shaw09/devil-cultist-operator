using BepInEx;

namespace Devil.Prepatch;

[BepInDependency("com.morebotsapiprepatch.tacticaltoaster", BepInDependency.DependencyFlags.HardDependency)]
[BepInPlugin(ModInfo.Guid, ModInfo.Name, ModInfo.Version)]
public sealed class Plugin : BaseUnityPlugin
{
}
