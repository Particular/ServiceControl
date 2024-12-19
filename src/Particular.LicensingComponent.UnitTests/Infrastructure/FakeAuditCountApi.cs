namespace Particular.LicensingComponent.UnitTests.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Api;

    class FakeAuditCountApi : IAuditCountApi
    {
        public Task<IList<ServiceControl.Api.Contracts.AuditCount>> GetEndpointAuditCounts(string endpoint, CancellationToken token) => throw new NotImplementedException();
    }
}