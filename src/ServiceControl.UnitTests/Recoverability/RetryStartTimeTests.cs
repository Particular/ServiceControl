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
        await retryingManager.Preparing("group-42", RetryType.FailureGroup, totalNumberOfMessages: 1);
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

    static FailureGroupsRetryController NewController(TestableMessageSession session, RetryingManager retryingManager, TimeProvider clock) =>
        new(session, retryingManager, new StubCurrentUserAccessor(new AuditUser("alice-sub", "Alice")), new RecordingMessageActionAuditLog(), clock);
}
