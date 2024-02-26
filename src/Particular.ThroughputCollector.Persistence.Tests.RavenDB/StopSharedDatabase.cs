namespace Particular.ThroughputCollector.Persistence.Tests.RavenDb
{
    using System.Threading.Tasks;
    using NUnit.Framework;

    [SetUpFixture]
    public class StopSharedDatabase
    {
        [OneTimeTearDown]
        public async Task Teardown() => await SharedEmbeddedServer.Stop();
    }
}