namespace ServiceControl.Api
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Api.Contracts;

    public interface IEndpointsApi
    {
        public Task<List<Endpoint>> GetEndpoints(CancellationToken cancellationToken);
    }
}