namespace ServiceControl.Audit.Persistence.Tests.MongoDB.AmazonDocumentDb
{
    using NUnit.Framework;
    using Infrastructure;
    using Shared;

    /// <summary>
    /// Lifecycle tests for Amazon DocumentDB.
    /// Requires AWS_DOCUMENTDB_CONNECTION_STRING environment variable to be set.
    /// Optionally set AWS_DOCUMENTDB_IS_ELASTIC=true for Elastic cluster testing.
    /// </summary>
    [TestFixture]
    [Category("AmazonDocumentDb")]
    [Explicit("Requires Amazon DocumentDB connection string via AWS_DOCUMENTDB_CONNECTION_STRING environment variable")]
    class LifecycleTests : LifecycleTestsBase
    {
        protected override IMongoTestEnvironment CreateEnvironment() => new AmazonDocumentDbEnvironment();

        [OneTimeSetUp]
        public void CheckEnvironmentAvailable()
        {
            if (!AmazonDocumentDbEnvironment.IsAvailable())
            {
                Assert.Ignore("Amazon DocumentDB connection string not configured. Set AWS_DOCUMENTDB_CONNECTION_STRING to run these tests.");
            }
        }
    }
}
