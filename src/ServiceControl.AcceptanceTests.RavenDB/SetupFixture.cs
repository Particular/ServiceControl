namespace ServiceControl.AcceptanceTests.RavenDB;

using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NUnit.Framework;

[SetUpFixture]
public class SetupFixture
{
    [OneTimeSetUp]
    public async Task Setup()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ServiceBus.Management.Infrastructure.Installers.EventSourceCreator.Create();
            await Task.Delay(3000);
        }
    }
}