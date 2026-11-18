namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.Loader;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using NUnit.Framework;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Hosting.Commands;
    using ServiceControl.Infrastructure;
    using ServiceControl.Operations;
    using ServiceControl.Persistence;

    class When_failed_error_imports_are_imported_again : AcceptanceTest
    {
        [Test]
        public async Task Should_store_the_sending_and_receiving_endpoints()
        {
            var settings = await CreateSettings();

            var sendingHostId = Guid.NewGuid();
            var receivingHostId = Guid.NewGuid();
            var nativeMessageId = Guid.NewGuid().ToString();
            var headers = new Dictionary<string, string>
            {
                [Headers.MessageId] = nativeMessageId,
                [Headers.EnclosedMessageTypes] = "Reimport.SomeCommand, Reimport, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                [Headers.OriginatingEndpoint] = "Reimport.Sender",
                [Headers.OriginatingMachine] = "SenderHost",
                [Headers.OriginatingHostId] = sendingHostId.ToString("N"),
                [Headers.ProcessingEndpoint] = "Reimport.Receiver",
                [Headers.HostDisplayName] = "ReceiverHost",
                [Headers.HostId] = receivingHostId.ToString("N"),
                ["NServiceBus.FailedQ"] = "Reimport.Receiver@ReceiverHost",
                ["NServiceBus.TimeOfFailure"] = DateTimeOffsetHelper.ToWireFormattedString(DateTimeOffset.UtcNow),
                ["NServiceBus.ExceptionInfo.ExceptionType"] = "System.InvalidOperationException",
                ["NServiceBus.ExceptionInfo.Message"] = "Simulated failure",
                ["NServiceBus.ExceptionInfo.StackTrace"] = "   at Reimport.Receiver.Handle()"
            };
            var failedMessageId = FailedErrorImport.DeriveKey(headers, nativeMessageId).ToString();

            IHost host;
            using (await StorageConfiguration.UseDatabaseLifecycleLock())
            {
                await new SetupCommand().Execute(new HostArguments([]), settings);

                host = ImportFailedErrorsCommand.BuildHost(settings);
                await host.StartAsync();
            }

            try
            {
                var failedImports = host.Services.GetRequiredService<IFailedErrorImportDataStore>();
                await failedImports.StoreFailedErrorImport(new FailedErrorImport
                {
                    Id = failedMessageId,
                    Message = new FailedTransportMessage { Id = nativeMessageId, Headers = headers, Body = "{}"u8.ToArray() },
                    ExceptionInfo = "Simulated import failure"
                });

                // RavenDB finds failed imports through an index, which may not have caught up with the write yet.
                await WaitFor(() => failedImports.QueryContainsFailedImports(), "the failed import to become visible");

                await host.Services.GetRequiredService<ImportFailedErrors>().Run();

                var failedMessage = await host.Services.GetRequiredService<IFailedMessageQueryDataStore>()
                    .GetLatestFailedMessageView(failedMessageId);

                Assert.That(failedMessage, Is.Not.Null, "the failed import was not stored as a failed message");
                using (Assert.EnterMultipleScope())
                {
                    Assert.That(failedMessage.SendingEndpoint?.Name, Is.EqualTo("Reimport.Sender"));
                    Assert.That(failedMessage.SendingEndpoint?.Host, Is.EqualTo("SenderHost"));
                    Assert.That(failedMessage.SendingEndpoint?.HostId, Is.EqualTo(sendingHostId));
                    Assert.That(failedMessage.ReceivingEndpoint?.Name, Is.EqualTo("Reimport.Receiver"));
                    Assert.That(failedMessage.ReceivingEndpoint?.Host, Is.EqualTo("ReceiverHost"));
                    Assert.That(failedMessage.ReceivingEndpoint?.HostId, Is.EqualTo(receivingHostId));
                }

                var monitoring = host.Services.GetRequiredService<IMonitoringDataStore>();

                // RavenDB reads known endpoints through an index, which may not have caught up with the import yet.
                await WaitFor(async () =>
                {
                    var knownEndpointNames = (await monitoring.GetAllKnownEndpoints()).Select(endpoint => endpoint.EndpointDetails.Name).ToArray();
                    return knownEndpointNames.Contains("Reimport.Sender") && knownEndpointNames.Contains("Reimport.Receiver");
                }, "Reimport.Sender and Reimport.Receiver to be recorded as known endpoints");
            }
            finally
            {
                await host.StopAsync();
                host.Dispose();
            }
        }

        static async Task WaitFor(Func<Task<bool>> condition, string description)
        {
            var timeout = Stopwatch.StartNew();

            while (timeout.Elapsed < TimeSpan.FromSeconds(30))
            {
                if (await condition())
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(200));
            }

            Assert.Fail($"Timed out waiting for {description}.");
        }

        async Task<Settings> CreateSettings()
        {
            var logPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(logPath);

            var settings = new Settings(TransportIntegration.TypeName, StorageConfiguration.PersistenceType,
                new LoggingSettings(Settings.SettingsRootNamespace, defaultLevel: LogLevel.Debug, logPath: logPath),
                forwardErrorMessages: false, errorRetentionPeriod: TimeSpan.FromDays(10))
            {
                InstanceName = $"Reimport.{Guid.NewGuid():n}",
                TransportConnectionString = TransportIntegration.ConnectionString,
                AssemblyLoadContextResolver = static _ => AssemblyLoadContext.Default
            };

            await StorageConfiguration.CustomizeSettings(settings);

            return settings;
        }
    }
}
