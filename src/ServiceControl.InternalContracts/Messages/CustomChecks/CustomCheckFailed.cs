﻿namespace ServiceControl.Contracts.CustomChecks
{
    using System;
    using NServiceBus;
    using Operations;

    public class CustomCheckFailed : IEvent
    {
        public string CustomCheckId { get; set; }
        public string Category { get; set; }
        public string FailureReason { get; set; }
        public DateTime FailedAt { get; set; }
        public EndpointDetails OriginatingEndpoint { get; set; }
        public Guid Id { get; set; }
    }
}
