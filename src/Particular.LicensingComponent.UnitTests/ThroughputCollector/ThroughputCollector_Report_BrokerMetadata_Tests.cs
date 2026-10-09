namespace Particular.LicensingComponent.UnitTests;

using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;
using Particular.LicensingComponent.UnitTests.Infrastructure;

/// <summary>
/// The report adds its environment data to the broker data. The data store can return a shared
/// instance, so the report must not change the broker data that the store holds. Otherwise two
/// reports that are generated at the same time write into the same dictionary.
/// </summary>
[TestFixture]
class ThroughputCollector_Report_BrokerMetadata_Tests : ThroughputCollectorTestFixture
{
    [Test]
    public async Task Should_not_change_the_stored_broker_data()
    {
        await DataStore.SaveBrokerMetadata(new BrokerMetadata("scope", new Dictionary<string, string> { ["Broker.Key"] = "value" }));

        var report = await ThroughputCollector.GenerateThroughputReport("1.2.3", null);

        var stored = await DataStore.GetBrokerMetadata();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData, Does.ContainKey("Broker.Key"), "The report includes the broker data");
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData, Does.ContainKey(EnvironmentDataType.ServicePulseVersion.ToString()));
            Assert.That(stored.Data.Keys, Is.EquivalentTo(new[] { "Broker.Key" }), "The report must not add its keys to the stored broker data");
        }
    }
}
