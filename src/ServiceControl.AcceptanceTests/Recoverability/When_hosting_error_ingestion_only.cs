namespace ServiceControl.AcceptanceTests.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.Loader;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.TestHost;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.ExternalIntegrations;
    using ServiceControl.Hosting.Commands;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;
    using ServiceControl.Operations;
    using ServiceControl.Persistence.EFCore.DbContexts;
    using ServiceControl.Persistence.EFCore.Entities;
    using ServiceControl.Persistence.EFCore.Infrastructure;
    using ServiceControl.Recoverability;
    using ServiceControl.Transports;

    class When_hosting_error_ingestion_only : AcceptanceTest
    {
        [Test]
        public async Task Should_ingest_without_an_endpoint_and_without_the_single_owner_services()
        {
            var settings = await CreateSettings();

            var host = ErrorIngestionOnlyCommand.BuildHost(settings);

            try
            {
                var hostedServices = host.Services.GetServices<IHostedService>()
                    .Select(hostedService => hostedService.GetType().Name)
                    .ToArray();

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(host.Services.GetService<IMessageSession>(), Is.Null,
                        "the host must not run an NServiceBus endpoint");
                    Assert.That(host.Services.GetService<ErrorIngestor>(), Is.Not.Null);

                    Assert.That(hostedServices, Is.EquivalentTo(ExpectedHostedServices),
                        "the set of hosted services in the error ingestion only host changed. Every one of "
                        + "these runs on every ingestion node, so decide whether that is safe before updating "
                        + "this list. ReturnToSenderDequeuer would steal messages from a retry batch, "
                        + "RetentionSweeper would duplicate the sweep, and EventDispatcherHostedService would "
                        + "publish every integration event once per node.");
                }
            }
            finally
            {
                await host.DisposeAsync();
            }
        }

        static readonly string[] ExpectedHostedServices =
        [
            "GenericWebHostService",                // health endpoint only, no ServiceControl API
            nameof(ErrorIngestion),                 // the reason this host exists
            "HeartbeatMonitoringHostedService",     // warms the endpoint monitor, does not check heartbeats
            "InternalCustomChecksHostedService",    // reports this node's ingestion health to the database
            "MetricsReporterHostedService",
            "ExternalIntegrationRequestsDataStore"  // its drain is inert here, nothing calls Subscribe
        ];

        [Test]
        public void Should_refuse_to_start_against_unsupported_storage()
        {
            var settings = new Settings(TransportIntegration.TypeName, "RavenDB", CreateLoggingSettings(),
                forwardErrorMessages: false, errorRetentionPeriod: TimeSpan.FromDays(10));

            var exception = Assert.ThrowsAsync<Exception>(() =>
                new ErrorIngestionOnlyCommand().Execute(new HostArguments([]), settings));

            Assert.That(exception.Message, Does.Contain("SQL Server or PostgreSQL"));
        }

        [Test]
        public async Task Should_ingest_a_failed_message_into_the_shared_database()
        {
            var settings = await CreateSettings();

            // The schema and the queues are provisioned by a normal instance, never by an ingest only host.
            await new SetupCommand().Execute(new HostArguments([]), settings);

            var messageId = Guid.NewGuid().ToString();
            var host = ErrorIngestionOnlyCommand.BuildHost(settings, builder => builder.WebHost.UseTestServer());

            try
            {
                await host.StartAsync();

                await DispatchFailedMessage(settings, messageId);

                var failedMessage = await WaitForFailedMessage(host, messageId);

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(failedMessage.Status, Is.EqualTo(FailedMessageStatus.Unresolved));
                    Assert.That(failedMessage.ExceptionType, Is.EqualTo("System.InvalidOperationException"));
                    Assert.That(failedMessage.ExceptionMessage, Is.EqualTo("Simulated failure"));
                    Assert.That(failedMessage.FailingEndpointAddress, Is.EqualTo("IngestOnly.Receiver@IngestOnlyHost"));

                    Assert.That(failedMessage.SendingEndpointName, Is.EqualTo("IngestOnly.Sender"));
                    Assert.That(failedMessage.SendingEndpointHost, Is.EqualTo("IngestOnlyHost"));
                    Assert.That(failedMessage.SendingEndpointHostId, Is.Not.Null);
                    Assert.That(failedMessage.ReceivingEndpointName, Is.EqualTo("IngestOnly.Receiver"));
                    Assert.That(failedMessage.ReceivingEndpointHost, Is.EqualTo("IngestOnlyHost"));
                    Assert.That(failedMessage.ReceivingEndpointHostId, Is.Not.Null);
                }

                await using var scope = host.Services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

                var groups = await dbContext.FailedMessageGroups.AsNoTracking()
                    .Where(group => group.FailedMessageUniqueId == failedMessage.UniqueMessageId)
                    .ToListAsync();
                var knownEndpoints = await dbContext.KnownEndpoints.AsNoTracking().ToListAsync();

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(groups.Select(group => group.Type), Does.Contain("Endpoint Name"));
                    Assert.That(groups.Select(group => group.Type), Does.Contain("Exception Type and Stack Trace"));
                    Assert.That(knownEndpoints.Select(endpoint => endpoint.Name), Does.Contain("IngestOnly.Receiver"));
                }

                await WaitFor(host, async dbContext => await dbContext.EventLogItems.AsNoTracking()
                        .AnyAsync(item => item.EventType == "MessageFailed" && item.Description == "Simulated failure"),
                    "an event log entry for the failure");
            }
            finally
            {
                await host.StopAsync();
                await host.DisposeAsync();
            }
        }

        static async Task DispatchFailedMessage(Settings settings, string messageId)
        {
            var dispatchSettings = new TransportSettings
            {
                EndpointName = "IngestOnly.Dispatcher",
                TransportType = settings.TransportType,
                ConnectionString = settings.TransportConnectionString,
                ErrorQueue = settings.ErrorQueue,
                MaxConcurrency = 1,
                AssemblyLoadContextResolver = settings.AssemblyLoadContextResolver
            };

            var customization = TransportFactory.Create(dispatchSettings);
            var infrastructure = await customization.CreateTransportInfrastructure("IngestOnly.Dispatcher", dispatchSettings);

            try
            {
                var headers = new Dictionary<string, string>
                {
                    [Headers.MessageId] = messageId,
                    [Headers.EnclosedMessageTypes] = "IngestOnly.SomeCommand, IngestOnly, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                    [Headers.ConversationId] = Guid.NewGuid().ToString(),
                    [Headers.OriginatingEndpoint] = "IngestOnly.Sender",
                    [Headers.OriginatingMachine] = "IngestOnlyHost",
                    [Headers.OriginatingHostId] = Guid.NewGuid().ToString("N"),
                    [Headers.ProcessingEndpoint] = "IngestOnly.Receiver",
                    [Headers.HostDisplayName] = "IngestOnlyHost",
                    [Headers.HostId] = Guid.NewGuid().ToString("N"),
                    ["NServiceBus.FailedQ"] = "IngestOnly.Receiver@IngestOnlyHost",
                    ["NServiceBus.TimeOfFailure"] = DateTimeOffsetHelper.ToWireFormattedString(DateTimeOffset.UtcNow),
                    ["NServiceBus.TimeSent"] = DateTimeOffsetHelper.ToWireFormattedString(DateTimeOffset.UtcNow),
                    ["NServiceBus.ExceptionInfo.ExceptionType"] = "System.InvalidOperationException",
                    ["NServiceBus.ExceptionInfo.Message"] = "Simulated failure",
                    ["NServiceBus.ExceptionInfo.Source"] = "IngestOnly",
                    ["NServiceBus.ExceptionInfo.StackTrace"] = "   at IngestOnly.Receiver.Handle()"
                };

                var outgoing = new OutgoingMessage(messageId, headers, "{}"u8.ToArray());

                await infrastructure.Dispatcher.Dispatch(
                    new TransportOperations(new TransportOperation(outgoing, new UnicastAddressTag(settings.ErrorQueue))),
                    new TransportTransaction());
            }
            finally
            {
                await infrastructure.Shutdown();
            }
        }

        static async Task WaitFor(IHost host, Func<ServiceControlDbContext, Task<bool>> condition, string description)
        {
            var scopeFactory = host.Services.GetRequiredService<IServiceScopeFactory>();
            var timeout = Stopwatch.StartNew();

            while (timeout.Elapsed < TimeSpan.FromSeconds(60))
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

                if (await condition(dbContext))
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(200));
            }

            Assert.Fail($"Timed out waiting for {description}.");
        }

        static async Task<FailedMessageEntity> WaitForFailedMessage(IHost host, string messageId)
        {
            var scopeFactory = host.Services.GetRequiredService<IServiceScopeFactory>();
            var timeout = Stopwatch.StartNew();

            while (timeout.Elapsed < TimeSpan.FromSeconds(60))
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

                var failedMessage = await dbContext.FailedMessages.AsNoTracking()
                    .SingleOrDefaultAsync(message => message.MessageId == messageId);

                if (failedMessage != null)
                {
                    return failedMessage;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(200));
            }

            Assert.Fail($"The failed message {messageId} was not ingested within the timeout.");
            return null;
        }

        async Task<Settings> CreateSettings()
        {
            var settings = new Settings(TransportIntegration.TypeName, StorageConfiguration.PersistenceType,
                CreateLoggingSettings(), forwardErrorMessages: false, errorRetentionPeriod: TimeSpan.FromDays(10))
            {
                InstanceName = $"IngestOnly.{Guid.NewGuid():n}",
                TransportConnectionString = TransportIntegration.ConnectionString,
                MaximumConcurrencyLevel = 2,
                AssemblyLoadContextResolver = static _ => AssemblyLoadContext.Default
            };

            await StorageConfiguration.CustomizeSettings(settings);

            return settings;
        }

        static LoggingSettings CreateLoggingSettings()
        {
            var logPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(logPath);
            return new LoggingSettings(Settings.SettingsRootNamespace, defaultLevel: LogLevel.Debug, logPath: logPath);
        }
    }
}
