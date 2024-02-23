namespace Particular.ThroughputCollector.Persistence.RavenDb;

//using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
//using Auditing.MessagesView;
//using Extensions;
//using Indexes;
//using Monitoring;
//using Raven.Client.Documents;
//using ServiceControl.Audit.Auditing;
//using ServiceControl.Audit.Infrastructure;
//using ServiceControl.Audit.Monitoring;
//using ServiceControl.Audit.Persistence.Infrastructure;
//using ServiceControl.SagaAudit;
//using Transformers;

class ThroughputDataStore(IRavenSessionProvider sessionProvider
    //, DatabaseConfiguration databaseConfiguration
    ) : IThroughputDataStore
{
    public async Task<QueryResult<IList<KnownEndpoint>>> QueryKnownEndpoints()
    {
        using var session = sessionProvider.OpenSession();

        var endpoints = await session.Advanced.LoadStartingWithAsync<KnownEndpoint>(KnownEndpoint.CollectionName, pageSize: 1024).ConfigureAwait(false);

        var knownEndpoints = endpoints
            .Select(x => new KnownEndpoint
            {
                //Id = DeterministicGuid.MakeId(x.Name, x.HostId.ToString()),
                //EndpointDetails = new EndpointDetails
                //{
                //    Host = x.Host,
                //    HostId = x.HostId,
                //    Name = x.Name
                //},
                //HostDisplayName = x.Host
            })
            .ToList();

        return new QueryResult<IList<KnownEndpoint>>(knownEndpoints, new QueryStatsInfo(string.Empty, knownEndpoints.Count));
    }
}