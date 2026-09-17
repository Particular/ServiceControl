#nullable enable
namespace ServiceControl.UnitTests.Migration.Fakes;

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Time.Testing;

// Signals each timer the engine creates, so a test only advances the clock once a pause is waiting on it.
public sealed class TimerRecordingTimeProvider : TimeProvider
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
