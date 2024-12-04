namespace Particular.LicensingComponent.UnitTests.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Api;
    using ServiceControl.Api.Contracts;

    class FakeEndpointApi : IEndpointsApi
    {
        public Task<List<Endpoint>> GetEndpoints(CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}