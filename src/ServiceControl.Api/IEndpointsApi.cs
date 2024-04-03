namespace ServiceControl.Api
{
    using System.Collections.Generic;
    using ServiceControl.Api.Contracts;

    public interface IEndpointsApi
    {
        public List<Endpoint> GetEndpoints();
    }
}
