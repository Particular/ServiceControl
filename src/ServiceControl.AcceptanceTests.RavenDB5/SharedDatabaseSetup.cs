using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.RavenDb5;

[SetUpFixture]
public class SharedDatabaseSetup
{
    public static EmbeddedDatabase SharedInstance { get; private set; }

    // Needs to be in a SetUpFixture otherwise the OneTimeSetUp is invoked for each inherited test fixture
    [OneTimeSetUp]
    public static async Task SetupSharedEmbeddedServer()
    {
        using (var cancellationSource = new CancellationTokenSource(10_000))
        {
            SharedInstance = await SharedEmbeddedServer.GetInstance();
        }
    }

    [OneTimeTearDown]
    public static void TearDown() => SharedInstance.Dispose();
}
