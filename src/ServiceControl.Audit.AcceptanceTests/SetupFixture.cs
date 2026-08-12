namespace ServiceControl.Audit.AcceptanceTests;

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Audit.Infrastructure;

[SetUpFixture]
public class SetupFixture
{
    [OneTimeSetUp]
    public async Task Setup()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        // Every test runs the setup command, which creates the event source if it is missing. Tests run in
        // parallel, so without creating it up front several of them race and all but one fail with
        // "Source ServiceControl.Audit already exists".
        EventSourceCreator.Create();

        await WaitForSource(EventSourceCreator.SourceName);
    }

    //There is a delay for this becoming true, tests will fall over if they interleave in the wrong way.
    [SupportedOSPlatform("windows")]
    static async Task WaitForSource(string sourceName)
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        while (!EventLog.SourceExists(sourceName))
        {
            cts.Token.ThrowIfCancellationRequested();
            await Task.Delay(500, CancellationToken.None);
        }
    }
}
