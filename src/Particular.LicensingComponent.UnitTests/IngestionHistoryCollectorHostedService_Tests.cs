namespace Particular.LicensingComponent.UnitTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuditThroughput;
using Contracts;
using Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NuGet.Versioning;
using NUnit.Framework;
using ServiceControl.Infrastructure.Ingestion;

[TestFixture]
class IngestionHistoryCollectorHostedService_Tests : ThroughputCollectorTestFixture
{
    public override Task Setup()
    {
        SetExtraDependencies = d => { };

        return base.Setup();
    }

    [Test]
    public async Task Second_poll_stores_the_delta_summed_across_audit_instances()
    {
        var start = new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.Zero);
        var fakeTimeProvider = new FakeTimeProvider(start);
        var processStart = start.UtcDateTime.AddDays(-3);
        var errorProvider = new QueuedErrorSnapshots(
            Snapshot(processStart, messages: 100, busySeconds: 10),
            Snapshot(processStart, messages: 160, busySeconds: 16));
        var auditQuery = new AuditQuery_WithIngestionSnapshots(
            [
                new AuditIngestionSnapshot("a", Snapshot(processStart, messages: 1000, busySeconds: 100)),
                new AuditIngestionSnapshot("b", Snapshot(processStart, messages: 2000, busySeconds: 200))
            ],
            [
                new AuditIngestionSnapshot("a", Snapshot(processStart, messages: 1300, busySeconds: 130)),
                new AuditIngestionSnapshot("b", Snapshot(processStart, messages: 2500, busySeconds: 260))
            ]);

        await RunTwoPolls(fakeTimeProvider, errorProvider, auditQuery);

        var history = await DataStore.GetIngestionHistory();

        Assert.That(history, Is.Not.Null);
        var errorDay = history.Days.Single(day => day.Source == IngestionHistory.ErrorSource);
        var auditDay = history.Days.Single(day => day.Source == IngestionHistory.AuditSource);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(errorDay.Date, Is.EqualTo(start.UtcDateTime.Date));
            Assert.That(errorDay.Messages, Is.EqualTo(60));
            Assert.That(errorDay.PeakHourMessages, Is.EqualTo(60));
            Assert.That(errorDay.PeakHourBusySeconds, Is.EqualTo(6).Within(0.001));
            Assert.That(auditDay.Messages, Is.EqualTo(800));
            Assert.That(auditDay.PeakHourMessages, Is.EqualTo(800));
            Assert.That(auditDay.PeakHourBusySeconds, Is.EqualTo(90).Within(0.001));
        }
    }

    [Test]
    public async Task A_restarted_process_contributes_its_whole_counters_as_the_delta()
    {
        var start = new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.Zero);
        var fakeTimeProvider = new FakeTimeProvider(start);
        var errorProvider = new QueuedErrorSnapshots(
            Snapshot(start.UtcDateTime.AddDays(-3), messages: 100, busySeconds: 10),
            Snapshot(start.UtcDateTime.AddMinutes(30), messages: 25, busySeconds: 5));
        var auditQuery = new AuditQuery_WithIngestionSnapshots([], []);

        await RunTwoPolls(fakeTimeProvider, errorProvider, auditQuery);

        var history = await DataStore.GetIngestionHistory();

        Assert.That(history, Is.Not.Null);
        var errorDay = history.Days.Single(day => day.Source == IngestionHistory.ErrorSource);
        Assert.That(errorDay.Messages, Is.EqualTo(25));
    }

    async Task RunTwoPolls(FakeTimeProvider fakeTimeProvider, QueuedErrorSnapshots errorProvider, AuditQuery_WithIngestionSnapshots auditQuery)
    {
        using var service = new IngestionHistoryCollectorHostedService(
            NullLogger<IngestionHistoryCollectorHostedService>.Instance, DataStore, auditQuery, fakeTimeProvider, errorProvider)
        { DelayStart = TimeSpan.Zero };

        await service.StartAsync(CancellationToken.None);

        await errorProvider.PolledOnce.Task.WaitAsync(TimeSpan.FromSeconds(30));
        fakeTimeProvider.Advance(TimeSpan.FromHours(1));
        await errorProvider.PolledTwice.Task.WaitAsync(TimeSpan.FromSeconds(30));

        var saveDeadline = DateTime.UtcNow.AddSeconds(30);

        while (await DataStore.GetIngestionHistory() is null && DateTime.UtcNow < saveDeadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20));
        }

        await service.StopAsync(CancellationToken.None);
    }

    static IngestionCountersSnapshot Snapshot(DateTime processStartUtc, long messages, double busySeconds) =>
        new(processStartUtc, messages, busySeconds, 0, 0, 0, 0, 0);

    class QueuedErrorSnapshots(params IngestionCountersSnapshot[] snapshots) : IErrorIngestionSnapshotProvider
    {
        public TaskCompletionSource PolledOnce { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource PolledTwice { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IngestionCountersSnapshot GetSnapshot()
        {
            var index = Math.Min(calls, snapshots.Length - 1);
            calls++;

            if (calls == 1)
            {
                PolledOnce.TrySetResult();
            }

            if (calls >= 2)
            {
                PolledTwice.TrySetResult();
            }

            return snapshots[index];
        }

        int calls;
    }

    class AuditQuery_WithIngestionSnapshots(List<AuditIngestionSnapshot> first, List<AuditIngestionSnapshot> second) : IAuditQuery
    {
        public SemanticVersion MinAuditCountsVersion => new(4, 29, 0);

        public Func<RemoteInstanceInformation, bool> ValidRemoteInstances => _ => true;

        public Task<List<AuditIngestionSnapshot>> GetAuditIngestionSnapshots(CancellationToken cancellationToken = default) =>
            Task.FromResult(calls++ == 0 ? first : second);

        public Task<List<Dictionary<string, string>>> GetAuditEnvironments(CancellationToken cancellationToken = default) =>
            Task.FromResult<List<Dictionary<string, string>>>([]);

        public Task<List<RemoteInstanceInformation>> GetAuditRemotes(CancellationToken cancellationToken = default) =>
            Task.FromResult<List<RemoteInstanceInformation>>([]);

        public Task<IEnumerable<ServiceControlEndpoint>> GetKnownEndpoints(CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<ServiceControlEndpoint>>([]);

        public Task<IEnumerable<AuditCount>> GetAuditCountForEndpoint(string endpointUrlName, CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<AuditCount>>([]);

        public Task<ConnectionSettingsTestResult> TestAuditConnection(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ConnectionSettingsTestResult { ConnectionSuccessful = true });

        int calls;
    }
}
