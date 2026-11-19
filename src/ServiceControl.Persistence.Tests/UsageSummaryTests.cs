namespace ServiceControl.Persistence.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Particular.LicensingComponent;
using Particular.LicensingComponent.Contracts;
using Particular.LicensingComponent.MonitoringThroughput;

// Kept outside Throughput/ because the in-memory test project compiles that folder and does not reference ThroughputCollector.
class UsageSummaryTests : PersistenceTestBase
{
    [Test]
    public async Task Reading_the_summary_keeps_endpoints_whose_sanitized_names_differ_only_in_case()
    {
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        var broker = new Endpoint("Smoke.Billing", ThroughputSource.Broker) { SanitizedName = "Smoke.Billing" };
        var monitoring = new Endpoint("smoke.billing", ThroughputSource.Monitoring) { SanitizedName = "smoke.billing" };
        await LicensingDataStore.SaveEndpoint(broker);
        await LicensingDataStore.SaveEndpoint(monitoring);
        await LicensingDataStore.RecordEndpointThroughput(broker.Id.Name, ThroughputSource.Broker, yesterday, 100);
        await LicensingDataStore.RecordEndpointThroughput(monitoring.Id.Name, ThroughputSource.Monitoring, yesterday, 30);

        // The summary never queries audit instances. The learning transport has no broker query.
        var collector = new ThroughputCollector(
            NullLogger<ThroughputCollector>.Instance,
            LicensingDataStore,
            new ThroughputSettings("Particular.ServiceControl", "error", "Learning", "TestCustomer", "6.0.0"),
            null,
            new MonitoringService(LicensingDataStore, TimeProvider.System),
            []);

        var summary = await collector.GetThroughputSummary();
        var remaining = await LicensingDataStore.GetAllEndpoints(true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(summary.Select(endpoint => (endpoint.Name, endpoint.MaxDailyThroughput)),
                Is.EquivalentTo(new[] { ("Smoke.Billing", 100L), ("smoke.billing", 30L) }));
            Assert.That(remaining.Select(endpoint => endpoint.Id), Is.EquivalentTo(new[] { broker.Id, monitoring.Id }));
        }
    }
}
