namespace ServiceControl.Launcher;

using System.Runtime.InteropServices;

// Values intentionally match the POSIX signal numbers used by Linux containers.
enum ShutdownSignal
{
    Interrupt = 2,
    Terminate = 15
}

sealed class ShutdownSignalSource : IDisposable
{
    readonly TaskCompletionSource<ShutdownSignal> requested = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly PosixSignalRegistration? interruptRegistration;
    readonly PosixSignalRegistration? terminateRegistration;

    public ShutdownSignalSource()
    {
        if (!OperatingSystem.IsWindows())
        {
            interruptRegistration = PosixSignalRegistration.Create(PosixSignal.SIGINT, context =>
            {
                context.Cancel = true;
                requested.TrySetResult(ShutdownSignal.Interrupt);
            });
            terminateRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
            {
                context.Cancel = true;
                requested.TrySetResult(ShutdownSignal.Terminate);
            });
        }

        Console.CancelKeyPress += OnCancelKeyPress;
    }

    public Task<ShutdownSignal> Requested => requested.Task;

    void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs eventArgs)
    {
        eventArgs.Cancel = true;
        requested.TrySetResult(ShutdownSignal.Interrupt);
    }

    public void Dispose()
    {
        Console.CancelKeyPress -= OnCancelKeyPress;
        interruptRegistration?.Dispose();
        terminateRegistration?.Dispose();
    }
}

sealed partial class UnixSignalSender : IChildSignalSender
{
    public void Send(IChildProcess child, ShutdownSignal signal)
    {
        ArgumentNullException.ThrowIfNull(child);

        // Console control events are delivered to attached children by Windows itself. The canonical
        // container is Linux, where the launcher must explicitly forward POSIX signals.
        if (OperatingSystem.IsWindows() || child.HasExited)
        {
            return;
        }

        if (Kill(child.ProcessId, (int)signal) != 0)
        {
            var error = Marshal.GetLastPInvokeError();
            // ESRCH means the child exited between the liveness check and signal delivery.
            if (error != 3)
            {
                throw new InvalidOperationException(
                    $"Failed to send {signal} to {child.Role} process {child.ProcessId} (errno {error}).");
            }
        }
    }

    [LibraryImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static partial int Kill(int processId, int signal);
}