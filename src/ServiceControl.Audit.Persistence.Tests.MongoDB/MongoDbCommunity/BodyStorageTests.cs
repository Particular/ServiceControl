namespace ServiceControl.Audit.Persistence.Tests.MongoDB.MongoDbCommunity
{
    using Infrastructure;
    using NUnit.Framework;
    using Shared;

    /// <summary>
    /// Body storage tests for MongoDB Community/Enterprise using Docker via Testcontainers.
    /// </summary>
    [TestFixture]
    [Category("MongoDbCommunity")]
    class BodyStorageTests : BodyStorageTestsBase
    {
        protected override IMongoTestEnvironment CreateEnvironment() => new MongoDbCommunityEnvironment();
    }
}
