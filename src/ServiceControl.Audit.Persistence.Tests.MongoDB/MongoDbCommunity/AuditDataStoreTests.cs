namespace ServiceControl.Audit.Persistence.Tests.MongoDB.MongoDbCommunity
{
    using NUnit.Framework;
    using Infrastructure;
    using Shared;

    /// <summary>
    /// AuditDataStore tests for MongoDB Community/Enterprise using Docker via Testcontainers.
    /// </summary>
    [TestFixture]
    [Category("MongoDbCommunity")]
    class AuditDataStoreTests : AuditDataStoreTestsBase
    {
        protected override IMongoTestEnvironment CreateEnvironment() => new MongoDbCommunityEnvironment();
    }
}
