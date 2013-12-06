namespace ServiceControl.MessageAuditing
{
    using System;

    public class MessageStatistics
    {
        public TimeSpan CriticalTime { get; set; }
        public TimeSpan ProcessingTime { get; set; }
    }
}