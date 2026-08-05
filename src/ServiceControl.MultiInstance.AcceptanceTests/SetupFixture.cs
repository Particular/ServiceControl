namespace ServiceControl.MultiInstance.AcceptanceTests;

using System.Runtime.InteropServices;
using NUnit.Framework;

[SetUpFixture]
public class SetupFixture
{
    [OneTimeSetUp]
    public void Setup()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ServiceBus.Management.Infrastructure.Installers.EventSourceCreator.Create();
        }
    }
}