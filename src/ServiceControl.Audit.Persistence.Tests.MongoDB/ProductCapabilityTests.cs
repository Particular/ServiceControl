namespace ServiceControl.Audit.Persistence.Tests.MongoDB
{
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.MongoDB.ProductCapabilities;

    [TestFixture]
    class ProductCapabilityTests
    {
        [Test]
        public void MongoDbCommunity_should_have_full_capabilities()
        {
            var capabilities = new MongoDbCommunityCapabilities();

            Assert.Multiple(() =>
            {
                Assert.That(capabilities.SupportsGridFS, Is.True);
                Assert.That(capabilities.SupportsTextIndexes, Is.True);
                Assert.That(capabilities.SupportsTransactions, Is.True);
                Assert.That(capabilities.SupportsTtlIndexes, Is.True);
                Assert.That(capabilities.SupportsChangeStreams, Is.True);
                Assert.That(capabilities.MaxDocumentSizeBytes, Is.EqualTo(16 * 1024 * 1024));
            });
        }

        [Test]
        public void AzureDocumentDb_should_have_limited_capabilities()
        {
            var capabilities = new AzureDocumentDbCapabilities();

            Assert.Multiple(() =>
            {
                // Azure DocumentDB does NOT support GridFS
                Assert.That(capabilities.SupportsGridFS, Is.False);
                // Azure DocumentDB supports text indexes (via TSVector)
                Assert.That(capabilities.SupportsTextIndexes, Is.True);
                Assert.That(capabilities.SupportsTransactions, Is.True);
                Assert.That(capabilities.SupportsTtlIndexes, Is.True);
                Assert.That(capabilities.SupportsChangeStreams, Is.True);
                Assert.That(capabilities.MaxDocumentSizeBytes, Is.EqualTo(16 * 1024 * 1024));
            });
        }

        [Test]
        public void AmazonDocumentDb_standard_should_have_most_capabilities()
        {
            var capabilities = new AmazonDocumentDbCapabilities(isElasticCluster: false);

            Assert.Multiple(() =>
            {
                // Standard Amazon DocumentDB supports GridFS
                Assert.That(capabilities.SupportsGridFS, Is.True);
                Assert.That(capabilities.SupportsTextIndexes, Is.True);
                Assert.That(capabilities.SupportsTransactions, Is.True);
                Assert.That(capabilities.SupportsTtlIndexes, Is.True);
                // Standard clusters support change streams
                Assert.That(capabilities.SupportsChangeStreams, Is.True);
                Assert.That(capabilities.MaxDocumentSizeBytes, Is.EqualTo(16 * 1024 * 1024));
            });
        }

        [Test]
        public void AmazonDocumentDb_elastic_should_have_reduced_capabilities()
        {
            var capabilities = new AmazonDocumentDbCapabilities(isElasticCluster: true);

            Assert.Multiple(() =>
            {
                // Elastic clusters do NOT support GridFS
                Assert.That(capabilities.SupportsGridFS, Is.False);
                Assert.That(capabilities.SupportsTextIndexes, Is.True);
                Assert.That(capabilities.SupportsTransactions, Is.True);
                Assert.That(capabilities.SupportsTtlIndexes, Is.True);
                // Elastic clusters do NOT support change streams
                Assert.That(capabilities.SupportsChangeStreams, Is.False);
                Assert.That(capabilities.MaxDocumentSizeBytes, Is.EqualTo(16 * 1024 * 1024));
            });
        }
    }
}
