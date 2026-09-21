namespace ServiceControl.Persistence.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NServiceBus.CustomChecks;
using NUnit.Framework;
using ServiceControl.Contracts.CustomChecks;
using ServiceControl.CustomChecks;
using ServiceControl.Infrastructure.DomainEvents;
using ServiceControl.Operations;
using ServiceControl.Persistence.EFCore.Infrastructure;
using ServiceControl.Persistence.EFCore.Infrastructure.Metrics;

class RetentionSweepCustomCheckTests : PersistenceTestBase
{
    RetentionSweeper Sweeper => ServiceProvider.GetRequiredService<RetentionSweeper>();
    RetentionSweepCustomCheck.State State => ServiceProvider.GetRequiredService<RetentionSweepCustomCheck.State>();

    RetentionSweepCustomCheck Check =>
        ServiceProvider.GetServices<ICustomCheck>().OfType<RetentionSweepCustomCheck>().Single();

    [Test]
    public async Task Check_initially_passes()
    {
        var result = await Check.PerformCheck();
        Assert.That(result, Is.EqualTo(CheckResult.Pass));
    }

    [Test]
    public async Task Check_fails_after_a_retention_failure()
    {
        State.ReportError(RetentionEntity.FailedMessages, "db timeout");

        var result = await Check.PerformCheck();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasFailed, Is.True);
            Assert.That(result.FailureReason, Does.Contain("FailedMessages"));
            Assert.That(result.FailureReason, Does.Contain("db timeout"));
        }
    }

    [Test]
    public async Task Check_passes_after_a_fully_successful_sweep()
    {
        State.ReportError(RetentionEntity.FailedMessages, "db timeout");

        await Sweeper.SweepNow();

        var result = await Check.PerformCheck();
        Assert.That(result, Is.EqualTo(CheckResult.Pass));
    }

    [Test]
    public async Task Check_does_not_expire_with_time()
    {
        State.ReportError(RetentionEntity.FailedMessages, "db timeout");
        AdvanceClock(TimeSpan.FromHours(2));

        var result = await Check.PerformCheck();
        Assert.That(result.HasFailed, Is.True);
    }

    [Test]
    public async Task Multiple_failing_entities_are_represented()
    {
        State.ReportError(RetentionEntity.FailedMessages, "body delete failed");
        State.ReportError(RetentionEntity.EventLog, "batch delete timeout");

        var result = await Check.PerformCheck();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasFailed, Is.True);
            Assert.That(result.FailureReason, Does.Contain("FailedMessages"));
            Assert.That(result.FailureReason, Does.Contain("EventLog"));
            Assert.That(result.FailureReason, Does.Contain("body delete failed"));
            Assert.That(result.FailureReason, Does.Contain("batch delete timeout"));
        }
    }

    [Test]
    public async Task Clearing_one_retention_type_keeps_other_failures()
    {
        State.ReportError(RetentionEntity.FailedMessages, "body delete failed");
        State.ReportError(RetentionEntity.EventLog, "batch delete timeout");

        State.Clear(RetentionEntity.FailedMessages);

        var result = await Check.PerformCheck();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.FailureReason, Does.Not.Contain("FailedMessages"));
            Assert.That(result.FailureReason, Does.Contain("EventLog"));
        }
    }

    [Test]
    public async Task Repeated_failed_results_raise_one_state_change_event()
    {
        State.ReportError(RetentionEntity.FailedMessages, "db timeout");

        var result = await Check.PerformCheck();
        var domainEvents = (FakeDomainEvents)ServiceProvider.GetRequiredService<IDomainEvents>();
        var processor = new CustomCheckResultProcessor(domainEvents, CustomChecks, NullLogger<CustomCheckResultProcessor>.Instance);
        var detail = new CustomCheckDetail
        {
            Category = "ServiceControl Health",
            CustomCheckId = "ServiceControl Retention",
            HasFailed = result.HasFailed,
            FailureReason = result.FailureReason,
            ReportedAt = Now,
            OriginatingEndpoint = new EndpointDetails
            {
                Host = "localhost",
                HostId = Guid.NewGuid(),
                Name = "ServiceControl"
            }
        };

        await processor.ProcessResult(detail);
        await processor.ProcessResult(detail);

        Assert.That(domainEvents.RaisedEvents.OfType<CustomCheckFailed>().Count(), Is.EqualTo(1));
    }
}