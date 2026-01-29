namespace ServiceControl.Audit.Persistence.Tests.MongoDB.MongoDbCommunity
{
    using NUnit.Framework;
    using Infrastructure;
    using Shared;

    /// <summary>
    /// UnitOfWork tests for MongoDB Community/Enterprise using Docker via Testcontainers.
    /// </summary>
    [TestFixture]
    [Category("MongoDbCommunity")]
    class UnitOfWorkTests : UnitOfWorkTestsBase
    {
        protected override IMongoTestEnvironment CreateEnvironment() => new MongoDbCommunityEnvironment();
    }
}
