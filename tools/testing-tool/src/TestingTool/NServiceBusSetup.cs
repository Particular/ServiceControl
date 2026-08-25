using Microsoft.Extensions.Options;
using NServiceBus;

namespace TestingTool;

/// <summary>
/// Extension method to configure the NServiceBus endpoint using the NServiceBus 10
/// <c>AddNServiceBusEndpoint</c> DI-integrated approach. The endpoint lifecycle is managed by
/// the ASP.NET Core host — no manual <c>Endpoint.Start</c>/<c>Endpoint.Stop</c> needed.
/// </summary>
public static class NServiceBusEndpointExtensions
{
    /// <summary>
    /// Registers the NServiceBus load-generation endpoint. Uses the Learning transport by default
    /// (sufficient for local/single-host testing); swap for a real transport when targeting a
    /// distributed ServiceControl deployment. Failed messages are routed to the ServiceControl
    /// error queue.
    /// </summary>
    public static IServiceCollection AddTestingToolEndpoint(this IServiceCollection services, TestingToolOptions options)
    {
        var config = new EndpointConfiguration("TestingTool.Load");

        // Learning transport — zero-config, single-host. For multi-container deployments,
        // replace with a real transport matching the ServiceControl instance under test.
        // Learning transport — routing to this endpoint is handled by SendOptions.RouteToThisEndpoint().
        config.UseTransport<LearningTransport>();

        // Route failures to the ServiceControl error queue.
        config.SendFailedMessagesTo(options.ErrorQueueName);

        // Simplified serializer; the testing tool generates volume, not complex payloads.
        config.UseSerialization<SystemJsonSerializer>();

        // Disable immediate retries to make error groups cleaner; deferred retries are handled
        // by ServiceControl's retry mechanism.
        var recoverability = config.Recoverability();
        recoverability.Immediate(im => im.NumberOfRetries(0));
        recoverability.Delayed(d => d.NumberOfRetries(0));

        config.EnableInstallers();

        services.AddNServiceBusEndpoint(config);
        return services;
    }
}