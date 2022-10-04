namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;

    [TestFixture]
    class BodyStorageTests : PersistenceTestFixture
    {
        [Test]
        public async Task Handles_no_results_gracefully()
        {
            var nonExistentBodyId = Guid.NewGuid().ToString();
            var result = await BodyStorage.TryFetch(nonExistentBodyId);

            Assert.That(result.HasResult, Is.False);
        }
    }
}