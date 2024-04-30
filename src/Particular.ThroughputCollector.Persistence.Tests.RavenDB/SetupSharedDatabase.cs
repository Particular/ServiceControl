namespace Particular.ThroughputCollector.Persistence.Tests;

using System.Threading.Tasks;
using NUnit.Framework;
using Particular.ThroughputCollector.Persistence.Tests.RavenDb;

[SetUpFixture]
public class SetupSharedDatabase
{
    [OneTimeSetUp]
    public async Task Setup() => await SharedEmbeddedServer.StartServer();

    [OneTimeTearDown]
    public async Task Teardown() => await SharedEmbeddedServer.StopServer();
}