namespace ServiceControl.Api.Contracts
{
    using System;

    public class RemoteIngestionCounters
    {
        public string ApiUri { get; set; }

        /// <summary>
        /// Null when the remote could not be reached or predates the environment endpoint.
        /// </summary>
        public RemoteIngestionCounterValues Counters { get; set; }
    }

    public class RemoteIngestionCounterValues
    {
        public DateTime ProcessStartUtc { get; set; }
        public long MessagesTotal { get; set; }
        public double BusySecondsTotal { get; set; }
        public double StorageSecondsTotal { get; set; }
        public long LagOverOneMinuteMessages { get; set; }
        public long LagOverTenMinutesMessages { get; set; }
        public long LagOverSixtyMinutesMessages { get; set; }
        public long LagKnownMessages { get; set; }
    }
}
