using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;

namespace Devil.Server;

[Injectable(TypePriority = OnLoadOrder.Preload + 1)]
public sealed class DevilCustomItems(
    WTTServerCommonLib.WTTServerCommonLib commonLib) : IOnLoad
{
    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var assembly = Assembly.GetExecutingAssembly();

        await commonLib.CustomItemServiceExtended.CreateCustomItems(assembly);
    }
}