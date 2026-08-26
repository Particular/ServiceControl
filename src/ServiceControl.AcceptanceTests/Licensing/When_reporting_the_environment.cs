namespace ServiceControl.AcceptanceTests.Licensing
{
    using System;
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

    class When_reporting_the_environment : AcceptanceTest
    {
        [Test]
        public async Task Should_describe_how_the_instance_is_deployed()
        {
            JsonDocument report = null;

            await Define<Context>()
                .WithEndpoint<MonitoringInstance>()
                .Do("Wait for the throughput data to be recorded", async _ =>
                {
                    var available = await this.TryGet<ReportGenerationState>(
                        "/api/licensing/report/available", state => state.ReportCanBeGenerated);

                    return available.HasResult;
                })
                .Do("Download the report", async _ =>
                {
                    var archive = await this.DownloadData("/api/licensing/report/file?spVersion=1.2.3");

                    report = ReadReport(archive);

                    return true;
                })
                .Done(_ => true)
                .Run();

            var data = report.RootElement
                .GetProperty("ReportData")
                .GetProperty("EnvironmentInformation")
                .GetProperty("EnvironmentData")
                .EnumerateObject()
                .ToDictionary(entry => entry.Name, entry => entry.Value.GetString());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(data.Keys, Is.SupersetOf(ExpectedKeys));

                Assert.That(data["Host.Model"], Is.AnyOf("Container", "WindowsService", "Console"));
                Assert.That(data["Persistence.Type"], Is.Not.Empty);
                Assert.That(data["Persistence.BodyStorage.Type"], Is.Not.Empty);
                Assert.That(data["Persistence.BodyStorage.Auth"], Is.AnyOf("ManagedIdentity", "SharedKeyOrSas", "IamRole", "StaticCredentials", "NotApplicable"));
                Assert.That(data["Security.Authentication"], Is.AnyOf("Enabled", "Disabled"));
                Assert.That(data["Features.EmailNotifications"], Is.AnyOf("Enabled", "Disabled", "NotConfigured"));
                Assert.That(int.Parse(data["Retention.ErrorHours"]), Is.GreaterThan(0));

                Assert.That(data.Values, Has.None.Contains(Environment.MachineName),
                    "The report must not carry anything that identifies the customer's machine");
            }
        }

        static readonly string[] ExpectedKeys =
        [
            "Host.Model",
            "Host.Orchestrator",
            "Host.OSPlatform",
            "Host.OSVersion",
            "Host.Architecture",
            "Host.RuntimeVersion",
            "Host.ProcessorCount",
            "Host.AvailableMemoryGB",
            "Persistence.Type",
            "Persistence.Hosting",
            "Persistence.ServerVersion",
            "Persistence.FullTextSearch",
            "Persistence.BodyStorage.Type",
            "Persistence.BodyStorage.Auth",
            "Security.Authentication",
            "Security.RoleBasedAuthorization",
            "Security.Https",
            "Features.IntegratedServicePulse",
            "Features.MessageEditing",
            "Features.ExternalIntegrationsPublishing",
            "Features.ForwardErrorMessages",
            "Features.EmailNotifications",
            "Retention.ErrorHours",
            "Retention.AuditHours",
            "Retention.EventsHours"
        ];

        static JsonDocument ReadReport(byte[] archive)
        {
            using var zip = new ZipArchive(new MemoryStream(archive), ZipArchiveMode.Read);
            using var entry = zip.Entries.Single().Open();

            return JsonDocument.Parse(entry);
        }

        const string SalesEndpoint = "Particular.Sales";

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
                        EndpointThroughputData = [new EndpointThroughputData { Name = SalesEndpoint, Throughput = 42 }]
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
