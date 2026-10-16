namespace ServiceControl.Persistence.Tests.Throughput;

using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;

[TestFixture]
class LicensedEndpointDetailsTests : PersistenceTestBase
{
    [Test]
    public async Task Should_return_null_when_endpoint_details_have_not_been_saved()
    {
        var retrievedDetails = await LicensingDataStore.GetLicensedEndpointDetails();

        Assert.That(retrievedDetails, Is.Null);
    }

    [Test]
    public async Task Should_return_null_when_only_an_amendment_has_been_saved()
    {
        await LicensingDataStore.SaveLicensedEndpointDetailsAmendment(
            new LicensedEndpointDetailsAmendment
            {
                RemovedEndpointIds = ["endpoint-id"]
            });

        var retrievedDetails = await LicensingDataStore.GetLicensedEndpointDetails();

        Assert.That(retrievedDetails, Is.Null);
    }

    [Test]
    public async Task Should_apply_endpoint_amendments()
    {
        await LicensingDataStore.SaveLicensedEndpointDetails(new LicensedEndpointDetails
        {
            Endpoints =
            [
                new LicensedEndpoint
                {
                    ClientId = "endpoint-id",
                    Name = "Original name",
                    Classification = 1,
                    Queues =
                    [
                        new QueueIdentity { NameHash = "original-queue" }
                    ]
                }
            ]
        });

        await LicensingDataStore.SaveLicensedEndpointDetailsAmendment(
            new LicensedEndpointDetailsAmendment
            {
                Endpoints =
                [
                    new LicensedEndpointAmendment
                    {
                        ClientId = "endpoint-id",
                        Name = "Amended name",
                        Classification = 2,
                        AmendedQueues =
                        [
                            new QueueIdentity { NameHash = "additional-queue" }
                        ]
                    }
                ]
            });

        var retrievedDetails = await LicensingDataStore.GetLicensedEndpointDetails();

        Assert.That(retrievedDetails, Is.Not.Null);
        var endpoint = retrievedDetails.Endpoints.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(endpoint.ClientId, Is.EqualTo("endpoint-id"));
            Assert.That(endpoint.Name, Is.EqualTo("Amended name"));
            Assert.That(endpoint.Classification, Is.EqualTo(2));
            Assert.That(endpoint.Queues.Select(q => q.NameHash), Is.EqualTo(["original-queue", "additional-queue"]));
        }
    }

    [Test]
    public async Task Should_remove_endpoints_listed_in_the_amendment()
    {
        await LicensingDataStore.SaveLicensedEndpointDetails(new LicensedEndpointDetails
        {
            Endpoints =
            [
                new LicensedEndpoint
                {
                    ClientId = "endpoint-to-keep",
                    Name = "Endpoint to keep"
                },
                new LicensedEndpoint
                {
                    ClientId = "endpoint-to-remove",
                    Name = "Endpoint to remove"
                }
            ]
        });

        await LicensingDataStore.SaveLicensedEndpointDetailsAmendment(
            new LicensedEndpointDetailsAmendment
            {
                RemovedEndpointIds = ["endpoint-to-remove"]
            });

        var retrievedDetails = await LicensingDataStore.GetLicensedEndpointDetails();

        Assert.That(retrievedDetails, Is.Not.Null);
        var endpoint = retrievedDetails.Endpoints.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(endpoint.ClientId, Is.EqualTo("endpoint-to-keep"));
            Assert.That(endpoint.Name, Is.EqualTo("Endpoint to keep"));
        }
    }

    [Test]
    public async Task Should_replace_an_existing_amendment()
    {
        await LicensingDataStore.SaveLicensedEndpointDetails(new LicensedEndpointDetails
        {
            Endpoints =
            [
                new LicensedEndpoint
                {
                    ClientId = "first-endpoint",
                    Name = "First endpoint"
                },
                new LicensedEndpoint
                {
                    ClientId = "second-endpoint",
                    Name = "Second endpoint"
                }
            ]
        });

        await LicensingDataStore.SaveLicensedEndpointDetailsAmendment(
            new LicensedEndpointDetailsAmendment
            {
                RemovedEndpointIds = ["first-endpoint"]
            });

        await LicensingDataStore.SaveLicensedEndpointDetailsAmendment(
            new LicensedEndpointDetailsAmendment
            {
                RemovedEndpointIds = ["second-endpoint"]
            });

        var retrievedDetails = await LicensingDataStore.GetLicensedEndpointDetails();

        Assert.That(retrievedDetails, Is.Not.Null);
        Assert.That(
            retrievedDetails.Endpoints.Select(endpoint => endpoint.ClientId),
            Is.EqualTo(["first-endpoint"]));
    }

    [Test]
    public async Task Saving_endpoint_details_should_clear_the_existing_amendment()
    {
        await LicensingDataStore.SaveLicensedEndpointDetails(new LicensedEndpointDetails
        {
            Endpoints =
            [
                new LicensedEndpoint
                {
                    ClientId = "endpoint-id",
                    Name = "Original name"
                }
            ]
        });

        await LicensingDataStore.SaveLicensedEndpointDetailsAmendment(
            new LicensedEndpointDetailsAmendment
            {
                RemovedEndpointIds = ["endpoint-id"]
            });

        await LicensingDataStore.SaveLicensedEndpointDetails(new LicensedEndpointDetails
        {
            Endpoints =
            [
                new LicensedEndpoint
                {
                    ClientId = "endpoint-id",
                    Name = "Replacement name"
                }
            ]
        });

        var retrievedDetails = await LicensingDataStore.GetLicensedEndpointDetails();

        Assert.That(retrievedDetails, Is.Not.Null);
        Assert.That(
            retrievedDetails.Endpoints.Single().Name,
            Is.EqualTo("Replacement name"));
    }
}