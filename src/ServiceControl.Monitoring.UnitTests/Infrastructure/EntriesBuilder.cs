namespace ServiceControl.Monitoring.UnitTests.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Messaging;
    using NUnit.Framework;

    public static class EntriesBuilder
    {
        public static RawMessage.Entry[] Build(Dictionary<DateTime, long> measurements)
        {
            var sortedMeasurements = measurements.OrderBy(kv => kv.Key).ToList();
            var message = new TaggedLongValueOccurrence();

            foreach (var kvp in sortedMeasurements)
            {
                Assert.True(message.TryRecord(kvp.Key.Ticks, kvp.Value));
            }

            return message.Entries;
        }
    }
}