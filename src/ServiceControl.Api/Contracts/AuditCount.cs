﻿namespace ServiceControl.Api.Contracts
{
    using System;

    public class AuditCount
    {
        public DateTime UtcDate { get; set; }
        public long Count { get; set; }
    }
}
