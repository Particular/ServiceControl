namespace ServiceControl.MultiInstance.AcceptanceTests;

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using AuditEventSourceCreator = global::ServiceControl.Audit.Infrastructure.EventSourceCreator;
using PrimaryEventSourceCreator = ServiceBus.Management.Infrastructure.Installers.EventSourceCreator;

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

        // These tests host a primary and an audit instance, so both event sources have to exist before
        // either instance runs its setup. The audit source used to be created as a side effect of the
        // audit acceptance tests running earlier in the same CI job, which is no longer the case.
        PrimaryEventSourceCreator.Create();
        AuditEventSourceCreator.Create();

        await WaitForSource(PrimaryEventSourceCreator.SourceName);
        await WaitForSource(AuditEventSourceCreator.SourceName);
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
