namespace ServiceControl.Launcher.UnitTests;

using System.Diagnostics;
using NUnit.Framework;

[TestFixture]
public class UnixSignalSenderTests
{
    [Test]
    public async Task Terminate_signal_is_delivered_to_a_real_child_process()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("POSIX signal delivery is used by the Linux container.");
        }

        var sleepExecutable = File.Exists("/bin/sleep") ? "/bin/sleep" : "/usr/bin/sleep";
        using var process = Process.Start(new ProcessStartInfo(sleepExecutable)
        {
            ArgumentList = { "30" },
            UseShellExecute = false
        }) ?? throw new InvalidOperationException("Failed to start the signal test process.");
        using var child = new SystemChildProcess(ContainerRole.Primary, process);

        try
        {
            new UnixSignalSender().Send(child, ShutdownSignal.Terminate);
            await child.WaitForExit().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            Assert.That(child.HasExited, Is.True);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
        }
    }
}
