namespace ServiceControl.Transport.Tests;

using System;

partial class ServiceControlPrimaryEndpointTests
{
    private static partial int GetTransportDefaultConcurrency() => Math.Max(8, Environment.ProcessorCount);
}