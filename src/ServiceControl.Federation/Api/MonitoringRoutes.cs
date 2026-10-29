namespace ServiceControl.Federation.Api;

using System.Collections.Generic;
using Members;
using S = Strategy;

/// <summary>
/// Every Monitoring instance route, relative to the Monitoring prefix, with the strategy that answers it.
/// </summary>
public static class MonitoringRoutes
{
    public static readonly IReadOnlyList<FederatedRoute> All =
    [
        Route(["GET"], "", S.Synthesised, MonitoringHandlers.Root) with { Anonymous = true },
        Route(["OPTIONS"], "", S.Synthesised, MonitoringHandlers.Options) with { Anonymous = true },
        Route(["GET"], "monitored-endpoints", S.KeyMerge, MonitoringHandlers.Endpoints),
        Route(["GET"], "monitored-endpoints/disconnected", S.KeyMerge, MonitoringHandlers.Disconnected),
        Route(["GET"], "monitored-endpoints/{endpointName}", S.KeyMerge, MonitoringHandlers.EndpointDetails),

        // Removal only drops the instance from the member's in-memory registry, which is harmless on a
        // member that does not know it, so it is broadcast rather than routed.
        Route(["DELETE"], "monitored-instance/{endpointName}/{instanceId}", S.Broadcast, (f, c) => f.Broadcast(c, Surface.Monitoring)),

        Route(["GET"], "connection", S.Lead, (f, c) => f.Lead(c, Surface.Monitoring)),
        Route(["GET"], "license", S.Synthesised, MonitoringHandlers.License),
        Route(["GET"], "api/my/routes", S.Synthesised, MonitoringHandlers.MyRoutes),
    ];

    static FederatedRoute Route(string[] methods, string template, string strategy, RouteHandler handler) => new(Surface.Monitoring, methods, template, strategy, handler);
}
