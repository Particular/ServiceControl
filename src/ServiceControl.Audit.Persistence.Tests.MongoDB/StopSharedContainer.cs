namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Threading.Tasks;
    using NUnit.Framework;

    /// <summary>
    /// Ensures the shared MongoDB container is stopped after all tests complete.
    /// </summary>
    [SetUpFixture]
    class StopSharedContainer
    {
        [OneTimeTearDown]
        public Task TearDown() => SharedMongoDbContainer.StopAsync();
    }
}
