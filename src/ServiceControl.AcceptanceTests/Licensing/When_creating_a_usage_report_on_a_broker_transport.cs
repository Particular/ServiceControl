namespace ServiceControl.AcceptanceTests.Licensing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using Particular.LicensingComponent.BrokerThroughput;
    using Particular.LicensingComponent.Contracts;
    using Particular.LicensingComponent.MonitoringThroughput;
    using Particular.LicensingComponent.Persistence;
    using Particular.LicensingComponent.Shared;
    using ServiceControl.Transports.BrokerThroughput;

    class When_creating_a_usage_report_on_a_broker_transport : AcceptanceTest
    {
        [Test]
        public async Task Should_report_what_the_broker_measured()
        {
            ReportGenerationState reportState = null;
            ThroughputConnectionSettings connectionSettings = null;
            ConnectionTestResults connectionTest = null;
            List<EndpointThroughputSummary> endpoints = null;
            JsonDocument report = null;

            // AddLicensingComponent only starts the broker collector when a query is already
            // registered, and it decides that while AddServiceControl runs.
            CustomizeHostBuilderBeforeServiceControl = builder =>
                builder.Services.AddSingleton<IBrokerThroughputQuery>(
                    new FakeBrokerThroughputQuery((SalesQueue, BrokerThroughput)));

            CustomizeHostBuilder = CollectFromTheBrokerImmediately;

            await Define<Context>()
                .WithEndpoint<MonitoringInstance>()
                .Do("Wait for the broker throughput to be collected", async _ =>
                {
                    var available = await this.TryGet<ReportGenerationState>(
                        "/api/licensing/report/available", state => state.ReportCanBeGenerated);

                    reportState = available.Item;

                    return available.HasResult;
                })
                .Do("Review where the numbers come from", async _ =>
                {
                    connectionSettings = await this.TryGet<ThroughputConnectionSettings>("/api/licensing/settings/info");
                    connectionTest = await this.TryGet<ConnectionTestResults>("/api/licensing/settings/test");
                })
                .Do("List the endpoints reporting throughput", async _ =>
                {
                    var summary = await this.TryGet<List<EndpointThroughputSummary>>(
                        "/api/licensing/endpoints",
                        items => items.Any(item => item.MaxDailyThroughput == BrokerThroughput));

                    endpoints = summary.Item;

                    return summary.HasResult;
                })
                .Do("Download the report", async _ =>
                {
                    var archive = await this.DownloadData("/api/licensing/report/file?spVersion=1.2.3");

                    report = ReadReport(archive);
                })
                .Done(_ => true)
                .Run();

            var reportData = report.RootElement.GetProperty("ReportData");
            var queues = reportData.GetProperty("Queues").EnumerateArray().ToArray();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(reportState.ReportCanBeGenerated, Is.True, reportState.Reason);

                Assert.That(connectionTest.BrokerConnectionResult.ConnectionSuccessful, Is.True,
                    $"The broker connection is only tested when a broker is configured: {connectionTest.BrokerConnectionResult.Diagnostics}");

                Assert.That(connectionSettings.BrokerSettings.Select(setting => setting.Name),
                    Has.Some.Contains(FakeBrokerThroughputQuery.SettingKey),
                    "The settings page lists the broker's own settings so the user can fill them in");

                Assert.That(reportData.GetProperty("ReportMethod").GetString(), Is.EqualTo("Broker"),
                    "Particular reads the report method to know how the numbers were measured");

                Assert.That(endpoints, Has.Exactly(1).Items,
                    "The broker queue and the monitored endpoint are one endpoint, not two");

                Assert.That(queues, Has.Length.EqualTo(1),
                    "The report counts the endpoint once, not once per source");

                var sales = queues.Single();

                Assert.That(sales.GetProperty("QueueName").GetString(), Is.EqualTo(SalesQueue),
                    "The report shows the name the user knows, not the sanitized one");

                Assert.That(sales.GetProperty("DailyThroughputFromBroker").GetArrayLength(), Is.EqualTo(1),
                    "Throughput measured at the broker has to be reported as coming from the broker");

                Assert.That(sales.GetProperty("DailyThroughputFromMonitoring").GetArrayLength(), Is.EqualTo(1),
                    "Throughput seen by monitoring has to be reported separately from the broker's");

                Assert.That(sales.GetProperty("Throughput").GetInt64(), Is.EqualTo(BrokerThroughput),
                    "The reported figure is the highest daily total across the sources");
            }
        }

        // The collector waits 40 seconds before its first pass, reachable only through the registration.
        static void CollectFromTheBrokerImmediately(IHostApplicationBuilder builder)
        {
            var scheduled = builder.Services.Single(registration =>
                registration.ServiceType == typeof(IHostedService) &&
                registration.ImplementationType == typeof(BrokerThroughputCollectorHostedService));

            builder.Services.Remove(scheduled);
            builder.Services.AddHostedService(provider => new BrokerThroughputCollectorHostedService(
                provider.GetRequiredService<ILogger<BrokerThroughputCollectorHostedService>>(),
                provider.GetRequiredService<IBrokerThroughputQuery>(),
                provider.GetRequiredService<ThroughputSettings>(),
                provider.GetRequiredService<ILicensingDataStore>(),
                provider.GetRequiredService<TimeProvider>())
            {
                DelayStart = TimeSpan.Zero
            });
        }

        static JsonDocument ReadReport(byte[] archive)
        {
            using var zip = new ZipArchive(new MemoryStream(archive), ZipArchiveMode.Read);
            using var entry = zip.Entries.Single().Open();

            return JsonDocument.Parse(entry);
        }

        const string SalesQueue = "Contoso/Sales";
        const long BrokerThroughput = 42;
        const long MonitoringThroughput = 17;

        class Context : ScenarioContext, ISequenceContext
        {
            public int Step { get; set; }
        }

        class MonitoringInstance : EndpointConfigurationBuilder
        {
            public MonitoringInstance() =>
                EndpointSetup<DefaultServerWithoutAudit>(c => c.EnableFeature<ReportThroughput>());

            class ReportThroughput : DispatchRawMessages<Context>
            {
                protected override TransportOperations CreateMessage(Context context)
                {
                    var recorded = new RecordEndpointThroughputData
                    {
                        StartDateTime = DateTime.UtcNow.AddDays(-1).AddHours(-1),
                        EndDateTime = DateTime.UtcNow.AddDays(-1),
                        EndpointThroughputData =
                        [
                            new EndpointThroughputData { Name = SalesQueue, Throughput = MonitoringThroughput }
                        ]
                    };

                    var body = JsonSerializer.SerializeToUtf8Bytes(recorded);
                    var message = new OutgoingMessage(Guid.NewGuid().ToString(), [], body);

                    return new TransportOperations(
                        new TransportOperation(message, new UnicastAddressTag(ServiceControlSettings.ServiceControlThroughputDataQueue)));
                }
            }
        }
    }
}
