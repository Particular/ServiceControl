namespace ServiceControl.Api
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ServiceControl.Api.Contracts;

    public interface IAuditCountApi
    {
        public Task<IList<AuditCount>> GetEndpointAuditCounts(int? page, int? pageSize, string endpoint);
    }
}
