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
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using Particular.LicensingComponent.Contracts;
    using Particular.LicensingComponent.MonitoringThroughput;
    using Particular.LicensingComponent.Shared;

    class When_creating_a_usage_report_on_a_non_broker_transport : AcceptanceTest
    {
        [Test]
        public async Task Should_report_the_corrected_and_redacted_throughput()
        {
            ReportGenerationState reportState = null;
            ThroughputConnectionSettings connectionSettings = null;
            ConnectionTestResults connectionTest = null;
            List<EndpointThroughputSummary> endpoints = null;
            List<string> masks = null;
            JsonDocument report = null;

            await Define<Context>()
                .WithEndpoint<MonitoringInstance>()
                .Do("Wait for the throughput data to be recorded", async _ =>
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
                        items => items.Any(item => item.Name == SalesEndpoint) && items.Any(item => item.Name == NotAnEndpoint));

                    endpoints = summary.Item;

                    return summary.HasResult;
                })
                .Do("Correct the queue that is not an NServiceBus endpoint", async _ =>
                    await this.Post("/api/licensing/endpoints/update", new[]
                    {
                        new UpdateUserIndicator
                        {
                            Name = NotAnEndpoint,
                            UserIndicator = nameof(UserIndicator.NotNServiceBusEndpoint)
                        }
                    }))
                .Do("Redact the customer name in the queue names", async _ =>
                {
                    await this.Post("/api/licensing/settings/masks/update", new[] { RedactedCustomer });

                    masks = await this.TryGet<List<string>>("/api/licensing/settings/masks", items => items.Contains(RedactedCustomer));

                    return masks != null;
                })
                .Do("Download the report", async _ =>
                {
                    var archive = await this.DownloadData("/api/licensing/report/file?spVersion=1.2.3");

                    report = ReadReport(archive);
                })
                .Done(_ => true)
                .Run();

            var queues = report.RootElement.GetProperty("ReportData").GetProperty("Queues").EnumerateArray().ToArray();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(reportState.ReportCanBeGenerated, Is.True, reportState.Reason);
                Assert.That(reportState.Transport, Is.Not.Empty, "ServicePulse shows the transport on the throughput page");

                Assert.That(connectionSettings, Is.Not.Null, "The settings page reads its content from settings/info");
                Assert.That(connectionTest.MonitoringConnectionResult.ConnectionSuccessful, Is.True,
                    $"Throughput arrived from monitoring, so the connection test should pass: {connectionTest.MonitoringConnectionResult.Diagnostics}");

                Assert.That(endpoints.Single(endpoint => endpoint.Name == SalesEndpoint).MaxDailyThroughput, Is.EqualTo(SalesThroughput),
                    "The endpoint list shows the throughput that was reported for the endpoint");

                Assert.That(masks, Does.Contain(RedactedCustomer));

                Assert.That(queues.Select(NameOf), Has.None.Contains(RedactedCustomer),
                    "A redacted name must not reach the report that is sent to Particular");

                Assert.That(queues.Select(NameOf).Any(name => name.Contains("Sales")), Is.True,
                    "Only the redacted part of the name is masked, the rest is still identifiable");

                Assert.That(UserIndicatorOf(queues.Single(queue => NameOf(queue).Contains("Website"))),
                    Is.EqualTo(nameof(UserIndicator.NotNServiceBusEndpoint)),
                    "The correction the user made in ServicePulse must survive into the report");
            }
        }

        static string NameOf(JsonElement queue) => queue.GetProperty("QueueName").GetString();

        static string UserIndicatorOf(JsonElement queue) => queue.GetProperty("UserIndicator").GetString();

        static JsonDocument ReadReport(byte[] archive)
        {
            using var zip = new ZipArchive(new MemoryStream(archive), ZipArchiveMode.Read);
            using var entry = zip.Entries.Single().Open();

            return JsonDocument.Parse(entry);
        }

        const string RedactedCustomer = "Particular";
        const string SalesEndpoint = "Particular.Sales";
        const string NotAnEndpoint = "Particular.Website.Inbox";
        const long SalesThroughput = 42;

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
                    // Yesterday, because a usage report only counts complete days.
                    var recorded = new RecordEndpointThroughputData
                    {
                        StartDateTime = DateTime.UtcNow.AddDays(-1).AddHours(-1),
                        EndDateTime = DateTime.UtcNow.AddDays(-1),
                        EndpointThroughputData =
                        [
                            new EndpointThroughputData { Name = SalesEndpoint, Throughput = SalesThroughput },
                            new EndpointThroughputData { Name = NotAnEndpoint, Throughput = 7 }
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
