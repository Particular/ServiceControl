namespace ServiceControl.Audit.Persistence.Tests.MongoDB.MongoDbCommunity
{
    using Infrastructure;
    using NUnit.Framework;
    using Shared;

    /// <summary>
    /// Full-text search tests for MongoDB Community/Enterprise using Docker via Testcontainers.
    /// </summary>
    [TestFixture]
    [Category("MongoDbCommunity")]
    class FullTextSearchTests : FullTextSearchTestsBase
    {
        protected override IMongoTestEnvironment CreateEnvironment() => new MongoDbCommunityEnvironment();
    }
}
