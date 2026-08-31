namespace ServiceControl.UnitTests.MessageRedirects;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Testing;
using NUnit.Framework;
using ServiceControl.MessageRedirects.Api;
using ServiceControl.Persistence.MessageRedirects;
using ServiceControl.UnitTests.Operations;

[TestFixture]
public class MessageRedirectsControllerClockTests
{
    static readonly DateTime FixedNow = new(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);

    [Test]
    public async Task Creating_a_redirect_stamps_the_injected_clock()
    {
        var store = new RecordingRedirectsStore();
        var controller = NewController(store);

        await controller.NewRedirects(new MessageRedirectsController.MessageRedirectRequest { FromPhysicalAddress = "A", ToPhysicalAddress = "B" });

        Assert.That(store.Redirects.Single().LastModified, Is.EqualTo(FixedNow));
    }

    [Test]
    public async Task Updating_a_redirect_stamps_the_injected_clock()
    {
        var existing = new MessageRedirect
        {
            FromPhysicalAddress = "A",
            ToPhysicalAddress = "B",
            LastModified = FixedNow.AddDays(-10)
        };

        var store = new RecordingRedirectsStore();
        store.Redirects.Add(existing);
        var controller = NewController(store);

        await controller.UpdateRedirect(existing.MessageRedirectId, new MessageRedirectsController.MessageRedirectRequest { ToPhysicalAddress = "C" });

        Assert.That(store.Redirects.Single().LastModified, Is.EqualTo(FixedNow));
    }

    static MessageRedirectsController NewController(IMessageRedirectsDataStore store) =>
        new(new TestableMessageSession(), store, new FakeDomainEvents(), new FixedClock(FixedNow));

    sealed class RecordingRedirectsStore : IMessageRedirectsDataStore
    {
        public List<MessageRedirect> Redirects { get; } = [];

        public Task<IReadOnlyList<MessageRedirect>> GetRedirects(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MessageRedirect>>(Redirects);

        public Task AddRedirect(MessageRedirect redirect, CancellationToken cancellationToken = default)
        {
            Redirects.Add(redirect);
            return Task.CompletedTask;
        }

        public Task UpdateRedirect(MessageRedirect redirect, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveRedirect(MessageRedirect redirect, CancellationToken cancellationToken = default)
        {
            Redirects.Remove(redirect);
            return Task.CompletedTask;
        }
    }

    sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);
    }
}
