namespace ServiceControl.Audit.Persistence.RavenDB.CustomChecks;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.CustomChecks;
using ServiceControl.RavenDB;

class CheckRavenDBSearchEngine(IRavenDocumentStoreProvider documentStoreProvider, DatabaseConfiguration databaseConfiguration) : CustomCheck("Audit Database Search Engine", "ServiceControl.Audit Health", TimeSpan.FromHours(1))
{
    public override async Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
    {
        var documentStore = await documentStoreProvider.GetDocumentStore(cancellationToken);

        var coraxIndexes = await StartupChecks.FindIndexesUsingCorax(documentStore, databaseConfiguration.Name, cancellationToken);

        return coraxIndexes.Length == 0
            ? CheckResult.Pass
            : CheckResult.Failed(StartupChecks.CoraxIndexesMessage(coraxIndexes.Select(i => $"{databaseConfiguration.Name}/{i}")));
    }
}
