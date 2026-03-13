namespace ServiceControl.Audit.Persistence.Tests.MongoDB.AzureDocumentDb
{
    using Infrastructure;
    using NUnit.Framework;
    using Shared;

    /// <summary>
    /// FailedAuditStorage tests for Azure DocumentDB.
    /// Requires AZURE_DOCUMENTDB_CONNECTION_STRING environment variable to be set.
    /// </summary>
    [TestFixture]
    [Category("AzureDocumentDb")]
    class FailedAuditStorageTests : FailedAuditStorageTestsBase
    {
        protected override IMongoTestEnvironment CreateEnvironment() => new AzureDocumentDbEnvironment();
    }
}
