namespace ServiceControl.Persistence.RavenDB.CustomChecks;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.CustomChecks;
using ServiceControl.RavenDB;

class CheckRavenDBSearchEngine(IRavenDocumentStoreProvider documentStoreProvider, RavenPersisterSettings settings) : CustomCheck("Error Database Search Engine", "ServiceControl Health", TimeSpan.FromHours(1))
{
    public override async Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
    {
        var documentStore = await documentStoreProvider.GetDocumentStore(cancellationToken);

        var coraxIndexes = new List<string>();

        foreach (var databaseName in new[] { settings.DatabaseName, settings.ThroughputDatabaseName })
        {
            foreach (var indexName in await StartupChecks.FindIndexesUsingCorax(documentStore, databaseName, cancellationToken))
            {
                coraxIndexes.Add($"{databaseName}/{indexName}");
            }
        }

        return coraxIndexes.Count == 0
            ? CheckResult.Pass
            : CheckResult.Failed(StartupChecks.CoraxIndexesMessage(coraxIndexes));
    }
}
