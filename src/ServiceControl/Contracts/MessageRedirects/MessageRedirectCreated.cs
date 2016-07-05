﻿namespace ServiceControl.Contracts.MessageRedirects
{
    using System;

    public class MessageRedirectCreated
    {
        public Guid MessageRedirectId { get; set; }
        public string FromPhysicalAddress { get; set; }
        public string ToPhysicalAddress { get; set; }
    }
}
