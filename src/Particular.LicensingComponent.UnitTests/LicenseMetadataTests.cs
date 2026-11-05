namespace Particular.LicensingComponent.UnitTests;

using System.Collections.Generic;
using System.Linq;
using Contracts;
using NUnit.Framework;
using Persistence;

[TestFixture]
class LicenseMetadataTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void Null_amendment_should_not_change_existing_amendments(bool hasExisting)
    {
        LicensedEndpointDetailsAmendment amendment = null;
        var existing = hasExisting ? CreateAmendment("existing") : null;

        Assert.DoesNotThrow(() => amendment.MergeWithExisting(existing));
        if (existing is not null)
        {
            AssertAmendment(existing, "existing");
        }
    }

    [Test]
    public void Missing_existing_amendment_should_leave_incoming_amendment_unchanged()
    {
        var amendment = CreateAmendment("incoming");

        amendment.MergeWithExisting(null);

        AssertAmendment(amendment, "incoming");
    }

    [Test]
    public void Merge_should_preserve_history_and_replace_round_tripped_collections()
    {
        var existing = CreateAmendment("existing");
        var amendment = CreateAmendment("incoming");

        amendment.MergeWithExisting(existing);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(amendment.UnmappedQueues, Is.EqualTo([Queue("existing-unmapped"), Queue("incoming-unmapped")]));
            Assert.That(amendment.RemovedEndpointIds, Is.EqualTo(["existing", "incoming"]));
            Assert.That(amendment.Endpoints, Has.Length.EqualTo(1));
            Assert.That(amendment.Endpoints[0].ClientId, Is.EqualTo("incoming"));
            Assert.That(amendment.InfrastructureQueues, Is.EqualTo([Queue("incoming-infrastructure")]));
            Assert.That(amendment.ExcludedQueues, Is.EqualTo([Queue("incoming-excluded")]));
            Assert.That(amendment.UnassignedQueues, Is.EqualTo([Queue("incoming-unassigned")]));
        }
        AssertAmendment(existing, "existing");
    }

    [Test]
    public void Empty_incoming_collections_should_preserve_only_non_round_tripped_history()
    {
        var existing = CreateAmendment("existing");
        var amendment = new LicensedEndpointDetailsAmendment();

        amendment.MergeWithExisting(existing);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(amendment.UnmappedQueues, Is.EqualTo(existing.UnmappedQueues));
            Assert.That(amendment.RemovedEndpointIds, Is.EqualTo(existing.RemovedEndpointIds));
            Assert.That(amendment.Endpoints, Is.Empty);
            Assert.That(amendment.InfrastructureQueues, Is.Empty);
            Assert.That(amendment.ExcludedQueues, Is.Empty);
            Assert.That(amendment.UnassignedQueues, Is.Empty);
        }
    }

    [Test]
    public void Empty_existing_amendment_should_leave_incoming_amendment_unchanged()
    {
        var amendment = CreateAmendment("incoming");

        amendment.MergeWithExisting(new LicensedEndpointDetailsAmendment());

        AssertAmendment(amendment, "incoming");
    }

    [TestCaseSource(nameof(QueueMoves))]
    public void Amend_should_apply_client_queue_moves(string source, string destination)
    {
        var queue = Queue("moving-queue", source == "discovered" ? null : source);
        var details = DetailsWithQueue(queue, source);
        var untouchedQueues = AllQueues(details).Where(q => q != queue).ToArray();
        var amendment = ClientAmendment(queue, destination);

        var amended = details.Amend(amendment);

        var expectedQueues = destination == "unmapped" || (source == "discovered" && destination == "unassigned")
            ? untouchedQueues
            : [.. untouchedQueues, queue];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(AllQueues(amended), Is.EquivalentTo(expectedQueues));
            Assert.That(QueuesAt(amended, destination), destination == "unmapped" || (source == "discovered" && destination == "unassigned")
                ? Does.Not.Contain(queue)
                : Does.Contain(queue));
            Assert.That(amended.LicenseId, Is.EqualTo("license-id"));
            Assert.That(amended.ServiceEndDate, Is.EqualTo("2026-12-31"));
        }
    }

    [TestCaseSource(nameof(SuccessiveQueueMoves))]
    public void Amend_should_apply_successive_client_saves(string source, string intermediate, string destination)
    {
        var queue = Queue("moving-queue", source);
        var existing = ClientAmendment(queue, intermediate);
        var savedQueue = intermediate == "unmapped" && destination != "unmapped" ? queue with { OriginalLocation = null } : queue;
        var amendment = ClientAmendment(savedQueue, destination);
        amendment.MergeWithExisting(existing);

        var amended = DetailsWithQueue(queue, source).Amend(amendment);

        var shouldBeSaved = destination != "unmapped" && (destination != "unassigned" || savedQueue.OriginalLocation is not null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(AllQueues(amended).Count(q => q.NameHash == queue.NameHash && q.ScopeHash == queue.ScopeHash), Is.EqualTo(shouldBeSaved ? 1 : 0));
            Assert.That(QueuesAt(amended, destination), shouldBeSaved ? Does.Contain(savedQueue) : Does.Not.Contain(savedQueue));
        }
    }

    [Test]
    public void Null_amendment_should_return_unchanged_details()
    {
        var details = DetailsWithQueue(Queue("moving-queue", "first"), "first");
        var queues = AllQueues(details).ToArray();

        var amended = details.Amend(null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(amended, Is.SameAs(details));
            Assert.That(AllQueues(amended), Is.EqualTo(queues));
            Assert.That(amended.Endpoints.All(endpoint => !endpoint.Amended), Is.True);
        }
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void Amend_should_apply_endpoint_name_and_classification_changes(bool rename, bool reclassify)
    {
        var queue = Queue("moving-queue", "first");
        var details = DetailsWithQueue(queue, "first");
        var amendment = new LicensedEndpointDetailsAmendment
        {
            Endpoints = [new LicensedEndpointAmendment
            {
                ClientId = "first",
                Name = rename ? "Renamed" : "first",
                Classification = reclassify ? 2 : 1
            }]
        };

        var amended = details.Amend(amendment);
        var endpoint = amended.Endpoints.Single(e => e.ClientId == "first");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(endpoint.Name, Is.EqualTo(rename ? "Renamed" : "first"));
            Assert.That(endpoint.Classification, Is.EqualTo(reclassify ? 2 : 1));
            Assert.That(endpoint.Amended, Is.True);
            Assert.That(endpoint.EndpointSize, Is.EqualTo("size"));
            Assert.That(endpoint.Queues, Does.Contain(queue));
            Assert.That(amended.Endpoints.Single(e => e.ClientId == "second").Amended, Is.False);
        }
    }

    [TestCase("unassigned")]
    [TestCase("unmapped")]
    [TestCase("second")]
    [TestCase("new-endpoint")]
    [TestCase("infrastructure")]
    [TestCase("excluded")]
    public void Amend_should_remove_endpoint_and_relocate_its_queues(string destination)
    {
        var queue = Queue("moving-queue", "first");
        var details = DetailsWithQueue(queue, "first");
        details.Endpoints.Single(e => e.ClientId == "first").Queues = [queue];
        var amendment = ClientAmendment(queue, destination);
        amendment.RemovedEndpointIds = ["first"];

        var amended = details.Amend(amendment);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(amended.Endpoints.Select(e => e.ClientId), Does.Not.Contain("first"));
            Assert.That(AllQueues(amended).Count(q => q == queue), Is.EqualTo(destination == "unmapped" ? 0 : 1));
            Assert.That(QueuesAt(amended, destination), destination == "unmapped" ? Does.Not.Contain(queue) : Does.Contain(queue));
        }
    }

    static IEnumerable<TestCaseData> QueueMoves()
    {
        foreach (var source in Sources)
        {
            foreach (var destination in Destinations)
            {
                if (source != "discovered" || destination != "unmapped")
                {
                    yield return new TestCaseData(source, destination);
                }
            }
        }
    }

    static IEnumerable<TestCaseData> SuccessiveQueueMoves()
    {
        foreach (var source in Sources.Where(s => s != "discovered"))
        {
            foreach (var intermediate in Destinations)
            {
                foreach (var destination in Destinations)
                {
                    yield return new TestCaseData(source, intermediate, destination);
                }
            }
        }
    }

    static LicensedEndpointDetails DetailsWithQueue(QueueIdentity queue, string source)
    {
        QueueIdentity[] OriginalQueues(string location) => source == location
            ? [queue, Queue(queue.NameHash, location, "other-scope")]
            : [Queue("untouched-" + location, location)];

        return new LicensedEndpointDetails
        {
            LicenseId = "license-id",
            ServiceEndDate = "2026-12-31",
            Endpoints = [Endpoint("first") with { Queues = OriginalQueues("first") }, Endpoint("second") with { Queues = OriginalQueues("second") }],
            InfrastructureQueues = OriginalQueues("infrastructure"),
            ExcludedQueues = OriginalQueues("excluded")
        };
    }

    static LicensedEndpoint Endpoint(string id) => new()
    {
        ClientId = id,
        Name = id,
        Classification = 1,
        EndpointSize = "size"
    };

    static LicensedEndpointDetailsAmendment ClientAmendment(QueueIdentity queue, string destination)
    {
        var amendment = new LicensedEndpointDetailsAmendment();
        if (destination == "unassigned")
        {
            amendment.UnassignedQueues = queue.OriginalLocation is null ? [] : [queue with { }];
        }
        else if (destination == "unmapped")
        {
            amendment.UnmappedQueues = [queue with { }];
        }
        else if (destination != queue.OriginalLocation)
        {
            switch (destination)
            {
                case "infrastructure":
                    amendment.InfrastructureQueues = [queue with { }];
                    break;
                case "excluded":
                    amendment.ExcludedQueues = [queue with { }];
                    break;
                default:
                    amendment.Endpoints = [new LicensedEndpointAmendment
                    {
                        ClientId = destination,
                        Name = destination,
                        Classification = 1,
                        AmendedQueues = [queue with { }]
                    }];
                    break;
            }
        }
        return amendment;
    }

    static IEnumerable<QueueIdentity> AllQueues(LicensedEndpointDetails details) =>
        details.Endpoints.SelectMany(e => e.Queues).Concat(details.InfrastructureQueues).Concat(details.ExcludedQueues).Concat(details.UnassignedQueues);

    static QueueIdentity[] QueuesAt(LicensedEndpointDetails details, string destination) => destination switch
    {
        "infrastructure" => details.InfrastructureQueues,
        "excluded" => details.ExcludedQueues,
        "unassigned" => details.UnassignedQueues,
        "unmapped" => [],
        _ => details.Endpoints.Single(e => e.ClientId == destination).Queues
    };

    static readonly string[] Sources = ["first", "second", "infrastructure", "excluded", "discovered"];
    static readonly string[] Destinations = ["first", "second", "new-endpoint", "infrastructure", "excluded", "unassigned", "unmapped"];

    static LicensedEndpointDetailsAmendment CreateAmendment(string id) => new()
    {
        Endpoints = [new LicensedEndpointAmendment { ClientId = id, Name = id, Classification = 1 }],
        RemovedEndpointIds = [id],
        InfrastructureQueues = [Queue($"{id}-infrastructure")],
        ExcludedQueues = [Queue($"{id}-excluded")],
        UnassignedQueues = [Queue($"{id}-unassigned")],
        UnmappedQueues = [Queue($"{id}-unmapped")]
    };

    static QueueIdentity Queue(string name, string location = "original-endpoint", string scope = "scope") => new()
    {
        NameHash = name,
        ScopeHash = scope,
        OriginalLocation = location
    };

    static void AssertAmendment(LicensedEndpointDetailsAmendment amendment, string id)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(amendment.Endpoints, Has.Length.EqualTo(1));
            Assert.That(amendment.Endpoints[0].ClientId, Is.EqualTo(id));
            Assert.That(amendment.Endpoints[0].Name, Is.EqualTo(id));
            Assert.That(amendment.Endpoints[0].Classification, Is.EqualTo(1));
            Assert.That(amendment.RemovedEndpointIds, Is.EqualTo([id]));
            Assert.That(amendment.InfrastructureQueues, Is.EqualTo([Queue($"{id}-infrastructure")]));
            Assert.That(amendment.ExcludedQueues, Is.EqualTo([Queue($"{id}-excluded")]));
            Assert.That(amendment.UnassignedQueues, Is.EqualTo([Queue($"{id}-unassigned")]));
            Assert.That(amendment.UnmappedQueues, Is.EqualTo([Queue($"{id}-unmapped")]));
        }
    }
}
