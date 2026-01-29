namespace ServiceControl.Audit.Persistence.Tests.MongoDB.AzureDocumentDb
{
    using NUnit.Framework;
    using Infrastructure;
    using Shared;

    /// <summary>
    /// UnitOfWork tests for Azure Cosmos DB for MongoDB.
    /// Requires Azure_CosmosDb_ConnectionString environment variable to be set.
    /// </summary>
    [TestFixture]
    [Category("AzureDocumentDb")]
    class UnitOfWorkTests : UnitOfWorkTestsBase
    {
        protected override IMongoTestEnvironment CreateEnvironment() => new AzureDocumentDbEnvironment();
    }
}
