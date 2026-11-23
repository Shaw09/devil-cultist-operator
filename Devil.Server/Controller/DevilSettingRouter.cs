using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Utils;

namespace Devil.Server.Controller;

[Injectable(TypePriority = OnLoadOrder.Routers + 1)]
public sealed class DevilSettingsRouter(
    JsonUtil jsonUtil,
    DevilServerConfigService configService)
    : StaticRouter(jsonUtil,
    [
        new RouteAction<EmptyRequestData>(
            "/devil-cultist-operator/config",
            (url, info, sessionId, output, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                string response = JsonSerializer.Serialize(
                    configService.GetClientSettings());

                return new ValueTask<string>(response);
            })
    ])
{
}