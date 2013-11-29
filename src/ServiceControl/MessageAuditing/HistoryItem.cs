namespace ServiceBus.Management.MessageAuditing
{
    using System;

    public class HistoryItem
    {
        public string Action { get; set; }

        public DateTime Time { get; set; }
    }
}