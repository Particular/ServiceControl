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
    public async Task Check_remains_passing_until_three_consecutive_failed_sweeps()
    {
        State.ReportError(RetentionEntity.FailedMessages, "db timeout");

        State.SweepComplete();
        Assert.That(await Check.PerformCheck(), Is.EqualTo(CheckResult.Pass));
        State.SweepComplete();
        Assert.That(await Check.PerformCheck(), Is.EqualTo(CheckResult.Pass));
        State.SweepComplete();

        var result = await Check.PerformCheck();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasFailed, Is.True);
            Assert.That(result.FailureReason, Does.Contain("FailedMessages"));
            Assert.That(result.FailureReason, Does.Contain("db timeout"));
            Assert.That(result.FailureReason, Does.Contain("https://docs.particular.net/servicecontrol/troubleshooting"));
        }
    }

    [Test]
    public async Task Successful_sweep_resets_the_consecutive_failure_count()
    {
        State.ReportError(RetentionEntity.FailedMessages, "db timeout");
        CompleteThreeSweeps();

        await Sweeper.SweepNow();
        State.ReportError(RetentionEntity.FailedMessages, "another timeout");
        State.SweepComplete();

        var result = await Check.PerformCheck();
        Assert.That(result, Is.EqualTo(CheckResult.Pass));
    }

    [Test]
    public async Task Check_does_not_expire_with_time()
    {
        State.ReportError(RetentionEntity.FailedMessages, "db timeout");
        CompleteThreeSweeps();
        AdvanceClock(TimeSpan.FromHours(2));

        var result = await Check.PerformCheck();
        Assert.That(result.HasFailed, Is.True);
    }

    [Test]
    public async Task Multiple_failing_entities_are_represented()
    {
        State.ReportError(RetentionEntity.FailedMessages, "body delete failed");
        State.ReportError(RetentionEntity.EventLog, "batch delete timeout");
        CompleteThreeSweeps();

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
        CompleteThreeSweeps();

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
        CompleteThreeSweeps();

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

    void CompleteThreeSweeps()
    {
        State.SweepComplete();
        State.SweepComplete();
        State.SweepComplete();
    }
}