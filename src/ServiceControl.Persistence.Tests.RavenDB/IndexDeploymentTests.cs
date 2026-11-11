namespace ServiceControl.Persistence.Tests.RavenDB;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations.Indexes;
using ServiceControl.RavenDB;

// IndexDeployment deploys all indexes in one atomic call, so one index that the server cannot compile blocks
// the deployment of every other index. Each index has to deploy on its own.
[TestFixture]
class IndexDeploymentTests
{
    [TestCaseSource(nameof(IndexTypes))]
    public async Task Index_deploys_to_a_clean_database(Type indexType)
    {
        await using var database = await ScratchDatabase.Create();
        var index = PrimaryIndexes.Create(indexType);

        Assert.That(await database.Store.Maintenance.SendAsync(new GetIndexOperation(index.IndexName)), Is.Null, "the database starts without the index");

        await IndexDeployment.CreateIndexesAsync([index], database.Store);

        var deployed = await database.Store.Maintenance.SendAsync(new GetIndexStatisticsOperation(index.IndexName));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deployed, Is.Not.Null);
            Assert.That(deployed.State, Is.EqualTo(IndexState.Normal));
            Assert.That(deployed.SearchEngineType, Is.EqualTo(SearchEngineType.Lucene));
        }
    }

    public static IEnumerable<TestCaseData> IndexTypes()
    {
        foreach (var type in PrimaryIndexes.Types)
        {
            yield return new TestCaseData(type).SetArgDisplayNames(type.Name);
        }
    }
}
