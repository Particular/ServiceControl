#nullable enable
namespace ServiceControl.UnitTests.Migration;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.UnitTests.Migration.Fakes;

[TestFixture]
class MigrationEngineThrottleTests
{
    static MigrationRow Row(string id) => new(id, new object(), new Dictionary<string, object?>());

    [Test]
    public async Task Optional_categories_pause_between_batches_for_the_configured_duration()
    {
        var category = MigrationCategoryRegistry.Find("EventLog")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("a"), Row("b"), Row("c"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 1 };
        var clock = new TimerRecordingTimeProvider();
        var pause = TimeSpan.FromSeconds(1);
        var options = new MigrationEngineOptions(pause, 5, 100, []);
        var engine = new MigrationEngine(source, target, checkpointStore, clock, options, NullLogger<MigrationEngine>.Instance);

        var runTask = engine.RunCategoryAsync(category);

        // Three batches, so a pause before the second and another before the third.
        for (var pauseNumber = 1; pauseNumber <= 2; pauseNumber++)
        {
            Assert.That(await clock.TimerCreated.WaitAsync(TimeSpan.FromSeconds(5)), Is.True, $"pause {pauseNumber} never started");
            Assert.That(runTask.IsCompleted, Is.False, $"pause {pauseNumber} should still be pending");
            clock.Advance(pause);
        }

        var checkpoint = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.CopiedCount, Is.EqualTo(3));
            Assert.That(clock.DueTimes, Is.EqualTo(new[] { pause, pause }));
        }
    }

    [Test]
    public async Task Required_categories_never_pause()
    {
        var category = MigrationCategoryRegistry.Find("KnownEndpoints")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("a"), Row("b"), Row("c"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 1 };
        // A FakeTimeProvider that is never advanced: if the engine tried to pause, this would hang
        // until the test runner's own timeout, which WaitAsync turns into a clear failure instead.
        var options = new MigrationEngineOptions(TimeSpan.FromSeconds(1), 5, 100, []);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var checkpoint = await engine.RunCategoryAsync(category).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.That(checkpoint.CopiedCount, Is.EqualTo(3));
    }

    // Signals each timer the engine creates, so the test only advances the clock once a pause is waiting on it.
    sealed class TimerRecordingTimeProvider : TimeProvider
    {
        readonly FakeTimeProvider clock = new();

        public SemaphoreSlim TimerCreated { get; } = new(0);

        public List<TimeSpan> DueTimes { get; } = [];

        public void Advance(TimeSpan delta) => clock.Advance(delta);

        public override DateTimeOffset GetUtcNow() => clock.GetUtcNow();

        public override long GetTimestamp() => clock.GetTimestamp();

        public override long TimestampFrequency => clock.TimestampFrequency;

        public override TimeZoneInfo LocalTimeZone => clock.LocalTimeZone;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = clock.CreateTimer(callback, state, dueTime, period);
            DueTimes.Add(dueTime);
            TimerCreated.Release();
            return timer;
        }
    }
}
