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
                    Name = "Endpoint to keep",
                    Classification = 0
                },
                new LicensedEndpoint
                {
                    ClientId = "endpoint-to-remove",
                    Name = "Endpoint to remove",
                    Classification = 0
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
    public async Task Should_preserve_previously_removed_endpoints_when_saving_another_amendment()
    {
        await LicensingDataStore.SaveLicensedEndpointDetails(new LicensedEndpointDetails
        {
            Endpoints =
            [
                new LicensedEndpoint
                {
                    ClientId = "first-endpoint",
                    Name = "First endpoint",
                    Classification = 0
                },
                new LicensedEndpoint
                {
                    ClientId = "second-endpoint",
                    Name = "Second endpoint",
                    Classification = 0
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
        Assert.That(retrievedDetails.Endpoints, Is.Empty);
    }

    [TestCase("endpoint-id", "other-endpoint")]
    [TestCase("endpoint-id", "new-endpoint")]
    [TestCase("endpoint-id", "infrastructure")]
    [TestCase("endpoint-id", "excluded")]
    [TestCase("endpoint-id", "unassigned")]
    [TestCase("endpoint-id", "unmapped")]
    [TestCase("infrastructure", "other-endpoint")]
    [TestCase("infrastructure", "new-endpoint")]
    [TestCase("infrastructure", "excluded")]
    [TestCase("infrastructure", "unassigned")]
    [TestCase("infrastructure", "unmapped")]
    [TestCase("excluded", "other-endpoint")]
    [TestCase("excluded", "new-endpoint")]
    [TestCase("excluded", "infrastructure")]
    [TestCase("excluded", "unassigned")]
    [TestCase("excluded", "unmapped")]
    public async Task Should_return_queue_to_original_location_after_save_and_refresh(string source, string destination)
    {
        var queue = new QueueIdentity { NameHash = "queue", ScopeHash = "scope", OriginalLocation = source };
        await LicensingDataStore.SaveLicensedEndpointDetails(DetailsWithQueue(queue));
        await LicensingDataStore.SaveLicensedEndpointDetailsAmendment(QueueAmendment(queue, destination));

        var firstRead = await LicensingDataStore.GetLicensedEndpointDetails();
        Assert.That(firstRead, Is.Not.Null);
        AssertQueueLocation(firstRead, queue, destination);

        var returningQueue = destination == "unmapped" ? queue with { OriginalLocation = null } : queue;
        await LicensingDataStore.SaveLicensedEndpointDetailsAmendment(QueueAmendment(returningQueue, source));

        var secondRead = await LicensingDataStore.GetLicensedEndpointDetails();
        Assert.That(secondRead, Is.Not.Null);
        AssertQueueLocation(secondRead, returningQueue, source);
    }

    [Test]
    public async Task Repeated_reads_should_not_duplicate_amended_queues_or_modify_base_details()
    {
        var queue = new QueueIdentity { NameHash = "queue", ScopeHash = "scope", OriginalLocation = "endpoint-id" };
        await LicensingDataStore.SaveLicensedEndpointDetails(DetailsWithQueue(queue));
        await LicensingDataStore.SaveLicensedEndpointDetailsAmendment(QueueAmendment(queue, "infrastructure"));

        for (var read = 0; read < 2; read++)
        {
            var amended = await LicensingDataStore.GetLicensedEndpointDetails();
            Assert.That(amended, Is.Not.Null);
            AssertQueueLocation(amended, queue, "infrastructure");
        }

        var original = await LicensingDataStore.GetLicensedEndpointDetails(amendWithUserChanges: false);
        Assert.That(original, Is.Not.Null);
        AssertQueueLocation(original, queue, "endpoint-id");
    }

    [TestCase("other-endpoint")]
    [TestCase("new-endpoint")]
    [TestCase("infrastructure")]
    [TestCase("excluded")]
    [TestCase("unassigned")]
    [TestCase("unmapped")]
    public async Task Removing_amendments_should_restore_base_details_after_read(string destination)
    {
        var queue = new QueueIdentity { NameHash = "queue", ScopeHash = "scope", OriginalLocation = "endpoint-id" };
        await LicensingDataStore.SaveLicensedEndpointDetails(DetailsWithQueue(queue));
        await LicensingDataStore.SaveLicensedEndpointDetailsAmendment(QueueAmendment(queue, destination));
        await LicensingDataStore.GetLicensedEndpointDetails();

        await LicensingDataStore.SaveLicensedEndpointDetailsAmendment(null);

        var restored = await LicensingDataStore.GetLicensedEndpointDetails();
        Assert.That(restored, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            AssertQueueLocation(restored, queue, "endpoint-id");
            Assert.That(restored.Endpoints.Select(e => e.ClientId), Is.EquivalentTo(new[] { "endpoint-id", "other-endpoint" }));
            Assert.That(restored.Endpoints.All(e => !e.Amended), Is.True);
        }
    }

    [Test]
    public async Task Should_preserve_non_round_tripped_history_while_replacing_other_collections()
    {
        var unmapped = new QueueIdentity { NameHash = "unmapped", OriginalLocation = "endpoint-id" };
        var moved = new QueueIdentity { NameHash = "moved", OriginalLocation = "infrastructure" };
        var details = DetailsWithQueue(unmapped);
        details.InfrastructureQueues = [moved];
        await LicensingDataStore.SaveLicensedEndpointDetails(details);
        await LicensingDataStore.SaveLicensedEndpointDetailsAmendment(new LicensedEndpointDetailsAmendment
        {
            RemovedEndpointIds = ["other-endpoint"],
            UnmappedQueues = [unmapped],
            ExcludedQueues = [moved]
        });
        await LicensingDataStore.GetLicensedEndpointDetails();

        await LicensingDataStore.SaveLicensedEndpointDetailsAmendment(new LicensedEndpointDetailsAmendment
        {
            UnassignedQueues = [moved],
            Endpoints = [new LicensedEndpointAmendment { ClientId = "endpoint-id", Name = "Renamed", Classification = 2 }]
        });

        var saved = await LicensingDataStore.GetLicensedEndpointDetailsAmendments();
        Assert.That(saved, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(saved.RemovedEndpointIds, Is.EqualTo(new[] { "other-endpoint" }));
            Assert.That(saved.UnmappedQueues, Is.EqualTo(new[] { unmapped }));
            Assert.That(saved.UnassignedQueues, Is.EqualTo(new[] { moved }));
            Assert.That(saved.ExcludedQueues, Is.Empty);
        }

        var amended = await LicensingDataStore.GetLicensedEndpointDetails();
        Assert.That(amended, Is.Not.Null);
        var endpoint = amended.Endpoints.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(endpoint.ClientId, Is.EqualTo("endpoint-id"));
            Assert.That(endpoint.Name, Is.EqualTo("Renamed"));
            Assert.That(endpoint.Classification, Is.EqualTo(2));
            Assert.That(endpoint.Amended, Is.True);
            Assert.That(endpoint.Queues, Is.Empty);
            AssertQueueLocation(amended, moved, "unassigned");
            AssertQueueLocation(amended, unmapped, "unmapped");
        }
    }

    static LicensedEndpointDetails DetailsWithQueue(QueueIdentity queue) => new()
    {
        Endpoints =
        [
            new LicensedEndpoint
            {
                ClientId = "endpoint-id", Name = "Original", Classification = 1,
                Queues = queue.OriginalLocation == "endpoint-id" ? [queue] : []
            },
            new LicensedEndpoint { ClientId = "other-endpoint", Name = "Other", Classification = 1 }
        ],
        InfrastructureQueues = queue.OriginalLocation == "infrastructure" ? [queue] : [],
        ExcludedQueues = queue.OriginalLocation == "excluded" ? [queue] : []
    };

    static LicensedEndpointDetailsAmendment QueueAmendment(QueueIdentity queue, string destination)
    {
        var amendment = new LicensedEndpointDetailsAmendment();
        if (queue.OriginalLocation == destination)
        {
            return amendment;
        }

        var apiQueue = queue with { };
        switch (destination)
        {
            case "infrastructure":
                amendment.InfrastructureQueues = [apiQueue];
                break;
            case "excluded":
                amendment.ExcludedQueues = [apiQueue];
                break;
            case "unassigned":
                amendment.UnassignedQueues = [apiQueue];
                break;
            case "unmapped":
                amendment.UnmappedQueues = [apiQueue];
                break;
            default:
                amendment.Endpoints = [new LicensedEndpointAmendment
                {
                    ClientId = destination, Name = destination, Classification = 1, AmendedQueues = [apiQueue]
                }];
                break;
        }
        return amendment;
    }

    static void AssertQueueLocation(LicensedEndpointDetails details, QueueIdentity queue, string location)
    {
        var allQueues = details.Endpoints.SelectMany(e => e.Queues)
            .Concat(details.InfrastructureQueues).Concat(details.ExcludedQueues).Concat(details.UnassignedQueues);
        var destination = location switch
        {
            "infrastructure" => details.InfrastructureQueues,
            "excluded" => details.ExcludedQueues,
            "unassigned" => details.UnassignedQueues,
            "unmapped" => [],
            _ => details.Endpoints.Single(e => e.ClientId == location).Queues
        };
        using (Assert.EnterMultipleScope())
        {
            Assert.That(allQueues.Count(q => q == queue), Is.EqualTo(location == "unmapped" ? 0 : 1));
            Assert.That(destination, location == "unmapped" ? Does.Not.Contain(queue) : Does.Contain(queue));
        }
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
                    Name = "Original name",
                    Classification = 0
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
                    Name = "Replacement name",
                    Classification = 0
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