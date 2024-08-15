namespace ServiceControl.Monitoring.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class HistoryPeriod
    {
        HistoryPeriod(TimeSpan value, short numberOfIntervals, short delayedIntervals)
        {
            Value = value;
            NumberOfIntervals = numberOfIntervals;
            DelayedIntervals = delayedIntervals;
            IntervalSize = TimeSpan.FromTicks(value.Ticks / NumberOfIntervals);
        }

        public TimeSpan Value { get; }

        public short NumberOfIntervals { get; }
        public short DelayedIntervals { get; }

        public TimeSpan IntervalSize { get; }

        public static HistoryPeriod FromMinutes(int minutes)
        {
            var period = All.FirstOrDefault(p => p.Value == TimeSpan.FromMinutes(minutes));
            if (period != null)
            {
                return period;
            }

            throw new Exception("Unknown history period.");
        }

        protected bool Equals(HistoryPeriod other) => Value.Equals(other.Value);

        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((HistoryPeriod)obj);
        }

        public override int GetHashCode() => Value.GetHashCode();

        public static IReadOnlyCollection<HistoryPeriod> All =
        [
            new(TimeSpan.FromMinutes(1), numberOfIntervals: 60, delayedIntervals: 2),
            new(TimeSpan.FromMinutes(5), numberOfIntervals: 60, delayedIntervals: 1),
            new(TimeSpan.FromMinutes(10), numberOfIntervals: 60, delayedIntervals: 1),
            new(TimeSpan.FromMinutes(15), numberOfIntervals: 60, delayedIntervals: 1),
            new(TimeSpan.FromMinutes(30), numberOfIntervals: 60, delayedIntervals: 1),
            new HistoryPeriod(TimeSpan.FromMinutes(LargestHistoryPeriod), 60, 1)
        ];

        public const int LargestHistoryPeriod = 60;
    }
}