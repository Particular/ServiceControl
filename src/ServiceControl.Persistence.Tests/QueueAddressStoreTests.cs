namespace ServiceControl.Persistence.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using Contracts.Operations;
using MessageFailures;
using NUnit.Framework;
using ServiceControl.Persistence.Infrastructure;

class QueueAddressStoreTests : PersistenceTestBase
{
    [Test]
    public async Task GetAddresses_groups_failed_messages_by_queue_address()
    {
        await SeedFailedMessages();

        await CompleteDatabaseOperation();

        var result = await QueueAddressStore.GetAddresses(new PagingInfo(1, 10));
        var addresses = result.Results;
        var physicalAddresses = addresses.Select(address => address.PhysicalAddress).OrderBy(address => address).ToArray();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.QueryStats.TotalCount, Is.EqualTo(4));
            Assert.That(physicalAddresses, Is.EqualTo(new[] { "alpha", "alpha-child", "beta", "gamma" }));
            Assert.That(addresses.Single(address => address.PhysicalAddress == "alpha").FailedMessageCount, Is.EqualTo(2));
            Assert.That(addresses.Single(address => address.PhysicalAddress == "alpha-child").FailedMessageCount, Is.EqualTo(1));
            Assert.That(addresses.Single(address => address.PhysicalAddress == "beta").FailedMessageCount, Is.EqualTo(1));
            Assert.That(addresses.Single(address => address.PhysicalAddress == "gamma").FailedMessageCount, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task GetAddressesBySearchTerm_filters_by_prefix()
    {
        await SeedFailedMessages();

        await CompleteDatabaseOperation();

        var result = await QueueAddressStore.GetAddressesBySearchTerm("alpha", new PagingInfo(1, 10));
        var addresses = result.Results;
        var physicalAddresses = addresses.Select(address => address.PhysicalAddress).OrderBy(address => address).ToArray();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.QueryStats.TotalCount, Is.EqualTo(2));
            Assert.That(physicalAddresses, Is.EqualTo(new[] { "alpha", "alpha-child" }));
            Assert.That(addresses.Sum(address => address.FailedMessageCount), Is.EqualTo(3));
        }
    }

    async Task SeedFailedMessages() =>
        await SeedFailedMessages(
            (new("7F21F22A-44B6-440C-851D-3524645FD083"), "alpha"),
            (new("E5DF7B16-648E-47F2-A9FD-F2BC1BA5D53C"), "alpha"),
            (new("8975FE11-DF21-438B-BB65-0006541CA73D"), "beta"),
            (new("7ED8EEA2-66EA-496C-922F-87D53A699228"), "alpha-child"),
            (new("D98BDA7B-B3D5-4CD8-A802-E38B57A58041"), "gamma"));

    public Task SeedFailedMessages(params (Guid MessageId, string EndpointAddress)[] failedMessages) =>
        PersistenceTestsContext.InsertFailedMessages(failedMessages.Select(f => new FailedMessage()
        {
            Id = f.MessageId.ToString(),
            UniqueMessageId = f.MessageId.ToString(),
            ProcessingAttempts = [
                new ()
                {
                    FailureDetails = new FailureDetails() { AddressOfFailingEndpoint = f.EndpointAddress,}
                }
            ]
        }).ToArray());
}