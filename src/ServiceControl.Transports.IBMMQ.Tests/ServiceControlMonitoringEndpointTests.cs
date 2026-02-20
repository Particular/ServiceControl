namespace ServiceControl.Transport.Tests;

using System;

partial class ServiceControlMonitoringEndpointTests
{
    private static partial int GetTransportDefaultConcurrency() => Math.Max(8, Environment.ProcessorCount);
}