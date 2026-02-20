namespace ServiceControl.Audit.Persistence.Tests.MongoDB.AmazonDocumentDb
{
    using Infrastructure;
    using NUnit.Framework;
    using Shared;

    /// <summary>
    /// Full-text search tests for Amazon DocumentDB.
    /// Requires AWS_DOCUMENTDB_CONNECTION_STRING environment variable.
    /// Note: Amazon DocumentDB only supports English text search.
    /// </summary>
    [TestFixture]
    [Category("AmazonDocumentDb")]
    class FullTextSearchTests : FullTextSearchTestsBase
    {
        protected override IMongoTestEnvironment CreateEnvironment() => new AmazonDocumentDbEnvironment();
    }
}
