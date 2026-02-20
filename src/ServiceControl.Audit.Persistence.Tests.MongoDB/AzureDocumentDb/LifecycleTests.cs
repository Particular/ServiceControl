namespace ServiceControl.Audit.Persistence.Tests.MongoDB.AzureDocumentDb
{
    using NUnit.Framework;
    using Infrastructure;
    using Shared;

    /// <summary>
    /// Lifecycle tests for Azure DocumentDB.
    /// Requires AZURE_DOCUMENTDB_CONNECTION_STRING environment variable to be set.
    /// </summary>
    [TestFixture]
    [Category("AzureDocumentDb")]
    [Explicit("Requires Azure DocumentDB connection string via AZURE_DOCUMENTDB_CONNECTION_STRING environment variable")]
    class LifecycleTests : LifecycleTestsBase
    {
        protected override IMongoTestEnvironment CreateEnvironment() => new AzureDocumentDbEnvironment();

        [OneTimeSetUp]
        public void CheckEnvironmentAvailable()
        {
            if (!AzureDocumentDbEnvironment.IsAvailable())
            {
                Assert.Ignore("Azure DocumentDB connection string not configured. Set AZURE_DOCUMENTDB_CONNECTION_STRING to run these tests.");
            }
        }
    }
}
