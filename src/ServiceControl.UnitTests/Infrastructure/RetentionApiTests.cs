namespace ServiceControl.UnitTests.Infrastructure;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Infrastructure.Api;
using ServiceControl.Persistence;

[TestFixture]
public class RetentionApiTests
{
    [Test]
    public async Task Every_sweep_outcome_is_reported_under_the_same_name([Values] RetentionSweepOutcome outcome)
    {
        var response = await GetStatus(outcome, "FailedMessages: boom");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.LastOutcome?.ToString(), Is.EqualTo(outcome.ToString()));
            Assert.That(response.LastError, Is.EqualTo("FailedMessages: boom"));
        }
    }

    [Test]
    public async Task A_running_sweep_reports_no_outcome()
    {
        var response = await GetStatus(null, null);

        Assert.That(response.LastOutcome, Is.Null);
    }

    static Task<ServiceControl.Api.Contracts.RetentionPurgeStatusResponse> GetStatus(RetentionSweepOutcome? outcome, string error)
    {
        var status = new RetentionSweepCurrentStatus(outcome is null, DateTime.UtcNow, null, null, null, outcome, error);
        var services = new ServiceCollection().AddSingleton<IRetentionSweeper>(new StubSweeper(status)).BuildServiceProvider();

        return new RetentionApi(services).GetStatus();
    }

    class StubSweeper(RetentionSweepCurrentStatus status) : IRetentionSweeper
    {
        public ManualSweepAttempt TryStartManualSweep(DateTime? errorCutoff, DateTime? eventsCutoff, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public RetentionSweepCurrentStatus GetStatus() => status;
    }
}
