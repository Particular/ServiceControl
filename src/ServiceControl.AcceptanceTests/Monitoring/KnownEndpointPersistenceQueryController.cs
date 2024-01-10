namespace ServiceControl.AcceptanceTests.Monitoring
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using ServiceControl.Persistence;

    [ApiController]
    [Route("api")]
    public class KnownEndpointPersistenceQueryController(IMonitoringDataStore dataStore) : ControllerBase
    {
        [Route("test/knownendpoints/query")]
        [HttpGet]
        public async Task<IReadOnlyList<KnownEndpoint>> GetKnownEndpoints() => await dataStore.GetAllKnownEndpoints();
    }
}