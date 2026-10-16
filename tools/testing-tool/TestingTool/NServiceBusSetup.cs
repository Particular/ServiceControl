using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using NServiceBus;
using TestingTool.Auditing;

namespace TestingTool;

/// <summary>
/// Extension method to configure the NServiceBus endpoint using the NServiceBus 10
/// <c>AddNServiceBusEndpoint</c> DI-integrated approach. The endpoint lifecycle is managed by
/// the ASP.NET Core host — no manual <c>Endpoint.Start</c>/<c>Endpoint.Stop</c> needed.
/// </summary>
public static class NServiceBusEndpointExtensions
{
    /// <summary>
    /// Registers the NServiceBus load-generation endpoint. The transport is chosen from the
    /// <c>transport</c> connection string (<c>ConnectionStrings__transport</c>, injected by the
    /// Aspire AppHost to match the ServiceControl platform transport):
    /// <list type="bullet">
    /// <item>starts with <c>amqp:</c> or <c>amqps:</c> — RabbitMQ, quorum queues with conventional routing</item>
    /// <item>a SQL Server connection string naming a database (<c>Initial Catalog</c>/<c>Database</c>) — SQL Server</item>
    /// <item>not set or empty — Learning transport, for local standalone runs</item>
    /// </list>
    /// Any other value throws, so a mistyped connection string can't silently send load to a
    /// Learning transport folder that ServiceControl never reads. The selected transport is
    /// registered as <see cref="SelectedTransport"/> so the host can log it at startup.
    /// Failed messages are routed to the ServiceControl error queue.
    /// </summary>
    public static IServiceCollection AddTestingToolEndpoint(this IServiceCollection services, TestingToolOptions options, IConfiguration configuration)
    {
        var config = new EndpointConfiguration("TestingTool.Load");

        var transportConnectionString = configuration.GetConnectionString("transport");
        string transportName;
        if (string.IsNullOrWhiteSpace(transportConnectionString))
        {
            transportName = "Learning";
            config.UseTransport<LearningTransport>();
        }
        else if (transportConnectionString.StartsWith("amqp:", StringComparison.OrdinalIgnoreCase)
                 || transportConnectionString.StartsWith("amqps:", StringComparison.OrdinalIgnoreCase))
        {
            transportName = "RabbitMQ";
            var transport = config.UseTransport<RabbitMQTransport>();
            transport.UseConventionalRoutingTopology(QueueType.Quorum);
            transport.ConnectionString(transportConnectionString);
        }
        else if (IsSqlServerConnectionString(transportConnectionString))
        {
            transportName = "SqlServer";
            config.UseTransport(new SqlServerTransport(transportConnectionString));
        }
        else
        {
            throw new InvalidOperationException(
                "Unrecognised 'transport' connection string (ConnectionStrings__transport). Expected an " +
                "amqp:// or amqps:// URI for RabbitMQ, or a SQL Server connection string with " +
                "Initial Catalog/Database for SQL Server. Leave it unset to use the Learning transport.");
        }

        services.AddSingleton(new SelectedTransport(transportName));

        // Route failures to the ServiceControl error queue.
        config.SendFailedMessagesTo(options.ErrorQueueName);
        config.AuditProcessedMessagesTo(options.AuditQueueName);

        // Lets the direct audit-queue writer replace the wire message after serialization, which is
        // the only stage late enough to control NServiceBus.EnclosedMessageTypes and the timestamps.
        config.Pipeline.Register(new RawAuditPayloadBehavior(), "Applies raw audit envelopes from the direct audit-queue writer");
        config.SendHeartbeatTo(options.ServiceControlInputQueue);
        
        // enable metrics
        var metrics = config.EnableMetrics();
        metrics.SendMetricDataToServiceControl(
            serviceControlMetricsAddress: options.MonitoringQueueName,
            interval: TimeSpan.FromSeconds(2)
        );

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

    static bool IsSqlServerConnectionString(string connectionString)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(new SqlConnectionStringBuilder(connectionString).InitialCatalog);
        }
        catch (ArgumentException)
        {
            // Not a key=value connection string, or contains keywords SqlClient doesn't know.
            return false;
        }
    }
}

/// <summary>The transport the load endpoint was configured with, for startup logging.</summary>
public sealed record SelectedTransport(string Name);
