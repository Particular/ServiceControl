﻿namespace ServiceControl.Recoverability
{
    using NServiceBus;

    public class RetryOperationCompleted : IEvent
    {
        public string RequestId { get; set; }
        public RetryType RetryType { get; set; }
        public bool Failed { get; set; }
        public double Progression { get; set; }
    }
}