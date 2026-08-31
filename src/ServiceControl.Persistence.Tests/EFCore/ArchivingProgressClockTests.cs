namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Infrastructure.DomainEvents;
using ServiceControl.Recoverability;

class ArchivingProgressClockTests : PersistenceTestBase
{
    readonly CapturingDomainEvents events = new();

    public ArchivingProgressClockTests() =>
        RegisterServices = services => services.AddSingleton<IDomainEvents>(events);

    [Test]
    public async Task Starting_an_archive_stamps_the_injected_clock()
    {
        AdvanceClock(TimeSpan.FromDays(7));

        await ArchiveMessages.StartArchiving("group-1", ArchiveType.FailureGroup);

        var starting = events.Raised.OfType<ArchiveOperationStarting>().Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(starting.StartTime, Is.EqualTo(Now));
            Assert.That(ArchiveMessages.GetArchivalOperations().Single().Started, Is.EqualTo(Now));
        }
    }

    [Test]
    public async Task Starting_an_unarchive_stamps_the_injected_clock()
    {
        AdvanceClock(TimeSpan.FromDays(7));

        await ArchiveMessages.StartUnarchiving("group-1", ArchiveType.FailureGroup);

        var starting = events.Raised.OfType<UnarchiveOperationStarting>().Single();

        Assert.That(starting.StartTime, Is.EqualTo(Now));
    }

    sealed class CapturingDomainEvents : IDomainEvents
    {
        public List<object> Raised { get; } = [];

        public Task Raise<T>(T domainEvent, CancellationToken cancellationToken = default) where T : IDomainEvent
        {
            Raised.Add(domainEvent);
            return Task.CompletedTask;
        }
    }
}
