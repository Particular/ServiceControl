namespace ServiceControl.Audit.Persistence.Tests.MongoDB.AzureDocumentDb
{
    using NUnit.Framework;
    using Infrastructure;
    using Shared;

    /// <summary>
    /// AuditDataStore tests for Azure DocumentDB.
    /// Requires AZURE_DOCUMENTDB_CONNECTION_STRING environment variable to be set.
    /// </summary>
    [TestFixture]
    [Category("AzureDocumentDb")]
    class AuditDataStoreTests : AuditDataStoreTestsBase
    {
        protected override IMongoTestEnvironment CreateEnvironment() => new AzureDocumentDbEnvironment();
    }
}
