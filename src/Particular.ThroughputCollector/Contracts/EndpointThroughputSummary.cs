﻿namespace Particular.ThroughputCollector.Contracts
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public class EndpointThroughputSummary
    {
        public string Name { get; set; }
        public bool IsKnownEndpoint { get; set; }
        public bool ThroughputExistsForThisPeriod { get; set; }
        public bool? UserIndicatedSendOnly { get; set; }
        public bool? UserIndicatedToIgnore { get; set; }
        public long MaxDailyThroughput { get; set; }
    }
}
