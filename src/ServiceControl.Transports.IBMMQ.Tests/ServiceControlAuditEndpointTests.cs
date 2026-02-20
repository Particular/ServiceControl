namespace ServiceControl.Transport.Tests;

using System;

partial class ServiceControlAuditEndpointTests
{
    private static partial int GetTransportDefaultConcurrency() => Math.Max(8, Environment.ProcessorCount);
}