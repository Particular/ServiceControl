namespace ServiceControl.Audit.Persistence.Tests.MongoDB.AmazonDocumentDb
{
    using Infrastructure;
    using NUnit.Framework;
    using Shared;

    /// <summary>
    /// Body storage tests for Amazon DocumentDB.
    /// Requires AWS_DOCUMENTDB_CONNECTION_STRING environment variable.
    /// </summary>
    [TestFixture]
    [Category("AmazonDocumentDb")]
    class BodyStorageTests : BodyStorageTestsBase
    {
        protected override IMongoTestEnvironment CreateEnvironment() => new AmazonDocumentDbEnvironment();
    }
}
