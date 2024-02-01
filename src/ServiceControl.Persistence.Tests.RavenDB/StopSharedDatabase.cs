namespace ServiceControl
{
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Persistence.Tests;

    [SetUpFixture]
    public class StopSharedDatabase
    {
        [OneTimeTearDown]
        public async Task Teardown() => await SharedEmbeddedServer.Stop();
    }
}