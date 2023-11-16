using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.RavenDB;

[SetUpFixture]
public class SharedDatabaseSetup
{
    public static EmbeddedDatabase SharedInstance { get; private set; }

    // Needs to be in a SetUpFixture otherwise the OneTimeSetUp is invoked for each inherited test fixture
    [OneTimeSetUp]
    public static async Task SetupSharedEmbeddedServer()
    {
        using (var cancellation = new CancellationTokenSource(60_000))
        {
            SharedInstance = await SharedEmbeddedServer.GetInstance(cancellation.Token);
        }
    }

    [OneTimeTearDown]
    public static void TearDown() => SharedInstance.Dispose();
}