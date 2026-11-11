namespace ServiceControl.Persistence.Tests.RavenDB.Throughput;

using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;
using ServiceControl.Persistence.RavenDB.Throughput;

[TestFixture]
class EndpointDocumentIdTests : RavenPersistenceTestBase
{
    [TestCase("Sales", "Sales/Audit")]
    [TestCase("Sales.Billing@machine", "Sales.Billing@machine/Audit")]
    [TestCase("Sales%01Billing", "Sales%01Billing/Audit")]
    [TestCase("Sales\\u0001Billing", "Sales\\u0001Billing/Audit")]
    [TestCase("Ünïcode Endpoint", "Ünïcode Endpoint/Audit")]
    public void Should_not_change_the_document_id_of_a_name_without_control_characters(string name, string expectedId) =>
        Assert.That(new EndpointIdentifier(name, ThroughputSource.Audit).GenerateDocumentId(), Is.EqualTo(expectedId));

    [TestCase("Sales\u0001Billing", "Sales%01Billing/Audit")]
    [TestCase("Sales\tBilling", "Sales%09Billing/Audit")]
    [TestCase("Sales\u007FBilling", "Sales%7FBilling/Audit")]
    [TestCase("Sales\u0085Billing", "Sales%85Billing/Audit")]
    [TestCase("\u0000Sales\u001F", "%00Sales%1F/Audit")]
    public void Should_escape_control_characters_in_the_document_id(string name, string expectedId) =>
        Assert.That(new EndpointIdentifier(name, ThroughputSource.Audit).GenerateDocumentId(), Is.EqualTo(expectedId));

    [Test]
    public async Task Endpoint_name_containing_a_control_character_can_be_stored_and_read_back_in_a_new_database()
    {
        var endpoint = new Endpoint("Endpoint\u0001Name", ThroughputSource.Audit);

        await LicensingDataStore.SaveEndpoint(endpoint);

        var endpoints = await LicensingDataStore.GetAllEndpoints(true);

        Assert.That(endpoints.Single().Id.Name, Is.EqualTo("Endpoint\u0001Name"));
    }
}
