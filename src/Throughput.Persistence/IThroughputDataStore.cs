namespace Throughput.Persistence;

//using System;
using System.Collections.Generic;
using System.Threading.Tasks;
//using ServiceControl.Audit.Auditing;
//using ServiceControl.Audit.Auditing.MessagesView;
//using ServiceControl.Audit.Infrastructure;
//using ServiceControl.Audit.Monitoring;
//using ServiceControl.SagaAudit;

public interface IThroughputDataStore
{
    Task<QueryResult<IList<KnownEndpoint>>> QueryKnownEndpoints();
}