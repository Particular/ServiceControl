namespace Particular.ThroughputCollector.UnitTests.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using ServiceControl.Api;

    class FakeEndpointApi : IEndpointsApi
    {
        public List<ServiceControl.Api.Contracts.Endpoint> GetEndpoints() => throw new NotImplementedException();
    }
}
