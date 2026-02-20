namespace ServiceControl.Audit.Persistence.Tests.MongoDB.AzureDocumentDb
{
    using Infrastructure;
    using NUnit.Framework;
    using Shared;

    /// <summary>
    /// Body storage tests for Azure Cosmos DB (MongoDB API).
    /// Requires AZURE_COSMOS_CONNECTION_STRING environment variable.
    /// </summary>
    [TestFixture]
    [Category("AzureDocumentDb")]
    class BodyStorageTests : BodyStorageTestsBase
    {
        protected override IMongoTestEnvironment CreateEnvironment() => new AzureDocumentDbEnvironment();
    }
}
