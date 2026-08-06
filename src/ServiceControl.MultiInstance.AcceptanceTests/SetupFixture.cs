namespace ServiceControl.MultiInstance.AcceptanceTests;

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
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

            //There is a delay for this becoming true, tests will fall over if they interleave in the wrong way.
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            while (!EventLog.SourceExists(ServiceBus.Management.Infrastructure.Installers.EventSourceCreator.SourceName))
            {
                cts.Token.ThrowIfCancellationRequested();
                await Task.Delay(500, CancellationToken.None);
            }
        }
    }
}