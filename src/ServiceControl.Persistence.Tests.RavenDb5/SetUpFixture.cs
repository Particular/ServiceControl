using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.RavenDb5;

[SetUpFixture]
public class SetUpFixture
{
    public static EmbeddedDatabase SharedInstance;
    // Needs to be in a SetUpFixture otherwise the OneTimeSetUp is invoked for each inherited test fixture
    [OneTimeSetUp]
    public static async Task SetupSharedEmbeddedServer() => SharedInstance = await SharedEmbeddedServer.GetInstance();

    [OneTimeTearDown]
    public static void TearDown()
    {
        SharedInstance.Dispose();
    }
}
