﻿namespace ServiceControl.Contracts.CustomChecks
{
    using System;
    using NServiceBus;
    using Operations;

    public class CustomCheckSucceeded : IEvent
    {
        public Guid Id { get; set; }

        public string CustomCheckId { get; set; }
        public string Category { get; set; }
        public DateTime SucceededAt { get; set; }
        public EndpointDetails OriginatingEndpoint { get; set; }

    }
}
