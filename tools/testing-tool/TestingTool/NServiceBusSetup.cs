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
    /// Registers the NServiceBus load-generation endpoint. When a RabbitMQ connection string is
    /// present (injected by the Aspire AppHost as <c>ConnectionStrings__rabbitmq</c>, matching the
    /// ServiceControl platform transport), the endpoint uses RabbitMQ with quorum/conventional
    /// routing. Otherwise it falls back to the Learning transport for local standalone runs.
    /// Failed messages are routed to the ServiceControl error queue.
    /// </summary>
    public static IServiceCollection AddTestingToolEndpoint(this IServiceCollection services, TestingToolOptions options, IConfiguration configuration)
    {
        var config = new EndpointConfiguration("TestingTool.Load");

        var rabbitConnectionString = configuration.GetConnectionString("transport");
        if (!string.IsNullOrWhiteSpace(rabbitConnectionString))
        {
            var transport = config.UseTransport<RabbitMQTransport>();
            transport.UseConventionalRoutingTopology(QueueType.Quorum);
            transport.ConnectionString(rabbitConnectionString);
        }
        else
        {
            config.UseTransport<LearningTransport>();
        }

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