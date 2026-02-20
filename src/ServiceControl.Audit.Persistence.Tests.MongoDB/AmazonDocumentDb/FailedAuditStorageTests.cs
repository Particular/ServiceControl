namespace ServiceControl.Audit.Persistence.Tests.MongoDB.AmazonDocumentDb
{
    using Infrastructure;
    using NUnit.Framework;
    using Shared;

    /// <summary>
    /// FailedAuditStorage tests for Amazon DocumentDB.
    /// Requires Amazon_DocumentDb_ConnectionString environment variable to be set.
    /// </summary>
    [TestFixture]
    [Category("AmazonDocumentDb")]
    class FailedAuditStorageTests : FailedAuditStorageTestsBase
    {
        protected override IMongoTestEnvironment CreateEnvironment() => new AmazonDocumentDbEnvironment();
    }
}
