namespace ServiceControl.Audit.Persistence.Tests.MongoDB.MongoDbCommunity
{
    using Infrastructure;
    using NUnit.Framework;
    using Shared;

    /// <summary>
    /// FailedAuditStorage tests for MongoDB Community/Enterprise using Docker via Testcontainers.
    /// </summary>
    [TestFixture]
    [Category("MongoDbCommunity")]
    class FailedAuditStorageTests : FailedAuditStorageTestsBase
    {
        protected override IMongoTestEnvironment CreateEnvironment() => new MongoDbCommunityEnvironment();
    }
}
