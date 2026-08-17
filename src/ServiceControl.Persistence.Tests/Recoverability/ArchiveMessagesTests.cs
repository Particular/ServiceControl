namespace ServiceControl.Persistence.Tests.Recoverability;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Infrastructure.DomainEvents;
using ServiceControl.Persistence.Recoverability;
using ServiceControl.Recoverability;

/// <summary>
/// Covers the in-memory state-management members of <see cref="IArchiveMessages" />
/// (<c>StartArchiving</c>/<c>StartUnarchiving</c>, <c>IsArchiveInProgressFor</c>,
/// <c>IsOperationInProgressFor</c>, <c>DismissArchiveOperation</c>, <c>GetArchivalOperations</c>)
/// that are not exercised by the archive/unarchive loop tests. These members drive the
/// API controllers and the group fetcher and have no dedicated coverage otherwise.
/// </summary>
[TestFixture]
class ArchiveMessagesTests : PersistenceTestBase
{
    readonly CapturingDomainEvents events = new();

    public ArchiveMessagesTests() =>
        RegisterServices = services => services.AddSingleton<IDomainEvents>(events);

    [Test]
    public async Task IsArchiveInProgressFor_is_false_when_no_operation_started()
    {
        Assert.That(ArchiveMessages.IsArchiveInProgressFor("group-1"), Is.False);
        await Task.CompletedTask;
    }

    [Test]
    public async Task StartArchiving_makes_IsArchiveInProgressFor_true()
    {
        await ArchiveMessages.StartArchiving("group-1", ArchiveType.FailureGroup);

        Assert.That(ArchiveMessages.IsArchiveInProgressFor("group-1"), Is.True);
    }

    [Test]
    public async Task StartArchiving_emits_ArchiveOperationStarting()
    {
        await ArchiveMessages.StartArchiving("group-1", ArchiveType.FailureGroup);

        var starting = events.Raised.OfType<ArchiveOperationStarting>().Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(starting.RequestId, Is.EqualTo("group-1"));
            Assert.That(starting.ArchiveType, Is.EqualTo(ArchiveType.FailureGroup));
        }
    }

    [Test]
    public async Task StartArchiving_registers_operation_in_GetArchivalOperations()
    {
        await ArchiveMessages.StartArchiving("group-1", ArchiveType.FailureGroup);

        var op = ArchiveMessages.GetArchivalOperations().Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(op.RequestId, Is.EqualTo("group-1"));
            Assert.That(op.ArchiveType, Is.EqualTo(ArchiveType.FailureGroup));
            Assert.That(op.GroupName, Is.EqualTo("Undefined"));
            Assert.That(op.NeedsAcknowledgement(), Is.False, "an in-progress op does not need acknowledgement");
        }
    }

    [Test]
    public async Task StartArchiving_twice_for_same_group_keeps_a_single_operation()
    {
        await ArchiveMessages.StartArchiving("group-1", ArchiveType.FailureGroup);
        await ArchiveMessages.StartArchiving("group-1", ArchiveType.FailureGroup);

        Assert.That(ArchiveMessages.GetArchivalOperations().Count(op => op.RequestId == "group-1"), Is.EqualTo(1));
    }

    [Test]
    public async Task StartArchiving_for_different_groups_registers_each_independently()
    {
        await ArchiveMessages.StartArchiving("group-1", ArchiveType.FailureGroup);
        await ArchiveMessages.StartArchiving("group-2", ArchiveType.FailureGroup);

        var ids = ArchiveMessages.GetArchivalOperations().Select(op => op.RequestId).ToArray();
        Assert.That(ids, Is.EquivalentTo(new[] { "group-1", "group-2" }));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ArchiveMessages.IsArchiveInProgressFor("group-1"), Is.True);
            Assert.That(ArchiveMessages.IsArchiveInProgressFor("group-2"), Is.True);
        }
    }

    [Test]
    public async Task DismissArchiveOperation_removes_the_operation()
    {
        await ArchiveMessages.StartArchiving("group-1", ArchiveType.FailureGroup);

        ArchiveMessages.DismissArchiveOperation("group-1", ArchiveType.FailureGroup);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ArchiveMessages.IsArchiveInProgressFor("group-1"), Is.False);
            Assert.That(ArchiveMessages.GetArchivalOperations(), Is.Empty);
        }
    }

    [Test]
    public void DismissArchiveOperation_does_not_throw_when_no_operation_exists()
    {
        Assert.DoesNotThrow(() => ArchiveMessages.DismissArchiveOperation("group-unknown", ArchiveType.FailureGroup));
    }

    [Test]
    public async Task StartUnarchiving_does_not_register_an_archive_operation()
    {
        await ArchiveMessages.StartUnarchiving("group-1", ArchiveType.FailureGroup);

        // GetArchivalOperations / IsArchiveInProgressFor track archive ops only, not unarchive ops.
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ArchiveMessages.GetArchivalOperations(), Is.Empty);
            Assert.That(ArchiveMessages.IsArchiveInProgressFor("group-1"), Is.False);
        }
    }

    [Test]
    public async Task StartUnarchiving_emits_UnarchiveOperationStarting()
    {
        await ArchiveMessages.StartUnarchiving("group-1", ArchiveType.FailureGroup);

        var starting = events.Raised.OfType<UnarchiveOperationStarting>().Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(starting.RequestId, Is.EqualTo("group-1"));
            Assert.That(starting.ArchiveType, Is.EqualTo(ArchiveType.FailureGroup));
        }
    }

    [Test]
    public async Task IsOperationInProgressFor_is_true_after_StartUnarchiving()
    {
        await ArchiveMessages.StartUnarchiving("group-1", ArchiveType.FailureGroup);

        Assert.That(ArchiveMessages.IsOperationInProgressFor("group-1", ArchiveType.FailureGroup), Is.True);
    }

    [Test]
    public async Task IsOperationInProgressFor_is_false_when_no_operation_started()
    {
        Assert.That(ArchiveMessages.IsOperationInProgressFor("group-1", ArchiveType.FailureGroup), Is.False);
        await Task.CompletedTask;
    }

    [Test]
    public async Task DismissArchiveOperation_only_removes_the_targeted_group()
    {
        await ArchiveMessages.StartArchiving("group-1", ArchiveType.FailureGroup);
        await ArchiveMessages.StartArchiving("group-2", ArchiveType.FailureGroup);

        ArchiveMessages.DismissArchiveOperation("group-1", ArchiveType.FailureGroup);

        var remaining = ArchiveMessages.GetArchivalOperations().Single();
        Assert.That(remaining.RequestId, Is.EqualTo("group-2"));
    }

    sealed class CapturingDomainEvents : IDomainEvents
    {
        public System.Collections.Generic.List<object> Raised { get; } = [];

        public Task Raise<T>(T domainEvent, CancellationToken cancellationToken = default) where T : IDomainEvent
        {
            Raised.Add(domainEvent);
            return Task.CompletedTask;
        }
    }
}