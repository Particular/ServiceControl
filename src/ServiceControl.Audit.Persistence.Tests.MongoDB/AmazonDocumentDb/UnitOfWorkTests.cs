namespace ServiceControl.Audit.Persistence.Tests.MongoDB.AmazonDocumentDb
{
    using NUnit.Framework;
    using Infrastructure;
    using Shared;

    /// <summary>
    /// UnitOfWork tests for Amazon DocumentDB.
    /// Requires Amazon_DocumentDb_ConnectionString environment variable to be set.
    /// </summary>
    [TestFixture]
    [Category("AmazonDocumentDb")]
    class UnitOfWorkTests : UnitOfWorkTestsBase
    {
        protected override IMongoTestEnvironment CreateEnvironment() => new AmazonDocumentDbEnvironment();
    }
}
