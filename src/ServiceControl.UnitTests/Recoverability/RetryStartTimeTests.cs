#nullable enable
namespace ServiceControl.UnitTests.Recoverability;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NServiceBus.Testing;
using NUnit.Framework;
using ServiceControl.Infrastructure.Auth;
using ServiceControl.Persistence;
using ServiceControl.Recoverability;
using ServiceControl.Recoverability.API;
using ServiceControl.UnitTests.Operations;

[TestFixture]
public class RetryStartTimeTests
{
    static readonly DateTimeOffset ClockStart = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Group_retry_takes_its_start_time_from_the_injected_clock()
    {
        var clock = new FakeTimeProvider(ClockStart);
        var session = new TestableMessageSession();
        var retryingManager = new RetryingManager(new FakeDomainEvents(), TestRetryMetrics.Create(clock), NullLogger<RetryingManager>.Instance, clock);

        await NewController(session, retryingManager, clock).ArchiveGroupErrors("group-42");

        var sent = (RetryAllInGroup)session.SentMessages.Single().Message;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sent.Started, Is.EqualTo(ClockStart.UtcDateTime));
            Assert.That(retryingManager.GetStatusForRetryOperation("group-42", RetryType.FailureGroup).Started, Is.EqualTo(ClockStart.UtcDateTime));
        }
    }

    [Test]
    public async Task A_completed_group_retry_never_finishes_before_it_started()
    {
        var clock = new FakeTimeProvider(ClockStart);
        var retryingManager = new RetryingManager(new FakeDomainEvents(), TestRetryMetrics.Create(clock), NullLogger<RetryingManager>.Instance, clock);

        await NewController(new TestableMessageSession(), retryingManager, clock).ArchiveGroupErrors("group-42");

        clock.Advance(TimeSpan.FromMinutes(5));
        await retryingManager.Preparing("group-42", RetryType.FailureGroup, totalNumberOfMessages: 1, clock.GetUtcNow().UtcDateTime);
        await retryingManager.PreparedBatch("group-42", RetryType.FailureGroup, numberOfMessagesPrepared: 1);
        await retryingManager.Forwarding("group-42", RetryType.FailureGroup);
        await retryingManager.ForwardedBatch("group-42", RetryType.FailureGroup, numberOfMessagesForwarded: 1);

        var operation = retryingManager.GetStatusForRetryOperation("group-42", RetryType.FailureGroup);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(operation.Started, Is.EqualTo(ClockStart.UtcDateTime));
            Assert.That(operation.CompletionTime, Is.EqualTo(ClockStart.UtcDateTime.AddMinutes(5)));
        }
    }

    [Test]
    public async Task A_retry_that_never_waited_records_when_it_was_asked_for()
    {
        var clock = new FakeTimeProvider(ClockStart);
        var retryingManager = NewManager(clock);

        await retryingManager.Preparing("selection-1", RetryType.MultipleMessages, totalNumberOfMessages: 1,
            clock.GetUtcNow().UtcDateTime, "all messages for endpoint Endpoint1");

        var operation = retryingManager.GetStatusForRetryOperation("selection-1", RetryType.MultipleMessages);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(operation.Started, Is.EqualTo(ClockStart.UtcDateTime), "only a group retry goes through Wait, so every other type has to be stamped here");
            Assert.That(operation.Originator, Is.EqualTo("all messages for endpoint Endpoint1"), "without this the screen labels a bulk retry as a selection of individual messages");
        }
    }

    [Test]
    public async Task A_completed_retry_that_runs_again_records_the_later_start()
    {
        var clock = new FakeTimeProvider(ClockStart);
        var retryingManager = NewManager(clock);
        await RunToCompletion(retryingManager, clock.GetUtcNow().UtcDateTime);

        clock.Advance(TimeSpan.FromHours(1));
        await RunToCompletion(retryingManager, clock.GetUtcNow().UtcDateTime);

        Assert.That(retryingManager.GetStatusForRetryOperation("selection-1", RetryType.MultipleMessages).Started,
            Is.EqualTo(ClockStart.UtcDateTime.AddHours(1)));
    }

    static async Task RunToCompletion(RetryingManager retryingManager, DateTime startTime)
    {
        await retryingManager.Preparing("selection-1", RetryType.MultipleMessages, totalNumberOfMessages: 1, startTime);
        await retryingManager.PreparedBatch("selection-1", RetryType.MultipleMessages, numberOfMessagesPrepared: 1);
        await retryingManager.Forwarding("selection-1", RetryType.MultipleMessages);
        await retryingManager.ForwardedBatch("selection-1", RetryType.MultipleMessages, numberOfMessagesForwarded: 1);
    }

    static RetryingManager NewManager(TimeProvider clock) =>
        new(new FakeDomainEvents(), TestRetryMetrics.Create(clock), NullLogger<RetryingManager>.Instance, clock);

    static FailureGroupsRetryController NewController(TestableMessageSession session, RetryingManager retryingManager, TimeProvider clock) =>
        new(session, retryingManager, new StubCurrentUserAccessor(new AuditUser("alice-sub", "Alice")), new RecordingMessageActionAuditLog(), clock);
}
