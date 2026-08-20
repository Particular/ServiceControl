namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.Infrastructure;

[TestFixture]
class QueueAddressVersionTests : IngestionTestBase
{
    [Test]
    public async Task Version_changes_when_a_queue_gains_a_failure_and_the_address_set_does_not()
    {
        await Ingest(Failure("SomeEndpoint@machine1"));
        await CompleteDatabaseOperation();

        var before = await QueueAddressStore.GetAddresses(new PagingInfo());

        // A second, different message failing on the SAME queue. The address set is unchanged, so a
        // validator built from the addresses alone cannot see this, and the client keeps a stale count.
        await Ingest(Failure("SomeEndpoint@machine1"));
        await CompleteDatabaseOperation();

        var after = await QueueAddressStore.GetAddresses(new PagingInfo());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(after.Results, Has.Count.EqualTo(1), "still one address");
            Assert.That(after.Results[0].FailedMessageCount, Is.EqualTo(2), "and the body now reports two failures");
            Assert.That(before.QueryStats.Version.HasValue, Is.True, "there was no version to move");
            Assert.That(after.QueryStats.Version.Matches(before.QueryStats.Version), Is.False,
                "the body changed, so the validator must too, or a revalidating client is served a stale count");
        }
    }

    [Test]
    public async Task Version_changes_when_a_message_moves_to_a_different_queue()
    {
        var first = Failure("SomeEndpoint@machine1");

        await Ingest(first);
        await CompleteDatabaseOperation();

        var before = await QueueAddressStore.GetAddresses(new PagingInfo());

        await Ingest(MovedTo("OtherEndpoint@machine2", first));
        await CompleteDatabaseOperation();

        var after = await QueueAddressStore.GetAddresses(new PagingInfo());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(after.Results, Has.Count.EqualTo(1), "still one address");
            Assert.That(after.Results[0].PhysicalAddress, Is.EqualTo("OtherEndpoint@machine2"), "and it is the new one");
            Assert.That(before.QueryStats.Version.HasValue, Is.True, "there was no version to move");
            Assert.That(after.QueryStats.Version.Matches(before.QueryStats.Version), Is.False,
                "the body reports a different address under the same count, so the validator cannot stay put");
        }
    }

    [Test]
    public async Task Version_changes_when_a_new_address_appears()
    {
        await Ingest(Failure("SomeEndpoint@machine1"));
        await CompleteDatabaseOperation();

        var before = await QueueAddressStore.GetAddresses(new PagingInfo());

        await Ingest(Failure("OtherEndpoint@machine2"));
        await CompleteDatabaseOperation();

        var after = await QueueAddressStore.GetAddresses(new PagingInfo());

        VersionAssert.Moved(before.QueryStats.Version, after.QueryStats.Version,
            "an address appeared, so a revalidating client must not be told its page is current");
    }

    [Test]
    public async Task Version_is_stable_while_nothing_changes()
    {
        await Ingest(Failure("SomeEndpoint@machine1"));
        await CompleteDatabaseOperation();

        var first = await QueueAddressStore.GetAddresses(new PagingInfo());
        var second = await QueueAddressStore.GetAddresses(new PagingInfo());

        VersionAssert.Held(first.QueryStats.Version, second.QueryStats.Version,
            "nothing changed, so the validator has to stay put or conditional GET never pays off");
    }

    [Test]
    public async Task An_empty_store_still_reports_a_version()
    {
        var result = await QueueAddressStore.GetAddresses(new PagingInfo());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Results, Is.Empty);
            Assert.That(result.QueryStats.Version.HasValue, Is.True,
                "an empty list is a representation like any other and has to be cacheable");
        }
    }

    static IngestedFailure Failure(string failingEndpointAddress) =>
        new() { FailingEndpointAddress = failingEndpointAddress };

    static IngestedFailure MovedTo(string failingEndpointAddress, IngestedFailure original) =>
        new()
        {
            MessageId = original.MessageId,
            EndpointName = original.EndpointName,
            FailingEndpointAddress = failingEndpointAddress,
            AttemptedAt = original.AttemptedAt.AddHours(1),
            TimeOfFailure = original.TimeOfFailure.AddHours(1)
        };
}
