namespace ServiceControl.Launcher;

using System.Diagnostics;

sealed class ChildProcessSupervisor(
    IChildProcessFactory processFactory,
    IChildSignalSender signalSender,
    TimeProvider timeProvider)
{
    public const int LauncherFailureExitCode = 1;

    public async Task<int> Run(
        LaunchPlan plan,
        Task<ShutdownSignal> shutdownRequested,
        TimeSpan shutdownTimeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(shutdownRequested);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(shutdownTimeout, TimeSpan.Zero);

        VerifyExecutables(plan.Children);

        var children = new List<IChildProcess>(plan.Children.Count);
        var exits = new List<Task>(plan.Children.Count);
        try
        {
            foreach (var childLaunch in plan.Children)
            {
                Console.WriteLine($"Starting {childLaunch.Descriptor.Role}: {childLaunch.Descriptor.ExecutablePath} (port {childLaunch.Descriptor.Port})");
                var child = processFactory.Start(childLaunch);
                children.Add(child);
                exits.Add(ObserveExit(child, cancellationToken));
            }

            await Task.WhenAny(exits.Append(shutdownRequested)).ConfigureAwait(false);

            if (shutdownRequested.IsCompleted)
            {
                var signal = await shutdownRequested.ConfigureAwait(false);
                Console.WriteLine($"Launcher received {signal}; stopping {children.Count} child process(es).");
                await StopChildren(children, signal, shutdownTimeout, cancellationToken).ConfigureAwait(false);
                return 0;
            }

            // Select the first role in canonical launch order when multiple children exit together.
            var exitedChild = children.First(child => child.HasExited);
            var exitCode = exitedChild.ExitCode;
            Console.Error.WriteLine($"{exitedChild.Role} exited unexpectedly with code {exitCode}; stopping remaining child processes.");
            await StopChildren(children, ShutdownSignal.Terminate, shutdownTimeout, cancellationToken).ConfigureAwait(false);

            return exitCode != 0 ? exitCode : plan.Children.Count == 1 ? 0 : LauncherFailureExitCode;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await StopChildren(children, ShutdownSignal.Terminate, shutdownTimeout, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch
        {
            await StopChildren(children, ShutdownSignal.Terminate, shutdownTimeout, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        finally
        {
            foreach (var child in children)
            {
                child.Dispose();
            }
        }
    }

    static void VerifyExecutables(IEnumerable<ChildLaunch> children)
    {
        foreach (var child in children)
        {
            if (!File.Exists(child.Descriptor.ExecutablePath))
            {
                throw new LauncherConfigurationException(
                    $"The {child.Descriptor.Role} executable was not found at '{child.Descriptor.ExecutablePath}'.");
            }
        }
    }

    static async Task ObserveExit(IChildProcess child, CancellationToken cancellationToken)
    {
        await child.WaitForExit(cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"{child.Role} exited with code {child.ExitCode}.");
    }

    async Task StopChildren(
        IReadOnlyCollection<IChildProcess> children,
        ShutdownSignal signal,
        TimeSpan shutdownTimeout,
        CancellationToken cancellationToken)
    {
        var running = children.Where(child => !child.HasExited).ToArray();
        foreach (var child in running)
        {
            signalSender.Send(child, signal);
        }

        if (running.Length == 0)
        {
            return;
        }

        var allExited = Task.WhenAll(running.Select(child => child.WaitForExit(CancellationToken.None)));
        var timeout = Task.Delay(shutdownTimeout, timeProvider, cancellationToken);
        if (await Task.WhenAny(allExited, timeout).ConfigureAwait(false) == allExited)
        {
            await allExited.ConfigureAwait(false);
            return;
        }

        foreach (var child in running.Where(child => !child.HasExited))
        {
            Console.Error.WriteLine($"{child.Role} did not stop within {shutdownTimeout}; killing its process tree.");
            child.KillTree();
        }

        await Task.WhenAll(running.Select(child => child.WaitForExit(CancellationToken.None))).ConfigureAwait(false);
    }
}

interface IChildProcessFactory
{
    IChildProcess Start(ChildLaunch child);
}

interface IChildProcess : IDisposable
{
    ContainerRole Role { get; }
    int ProcessId { get; }
    bool HasExited { get; }
    int ExitCode { get; }
    Task WaitForExit(CancellationToken cancellationToken = default);
    void KillTree();
}

interface IChildSignalSender
{
    void Send(IChildProcess child, ShutdownSignal signal);
}

sealed class SystemChildProcessFactory : IChildProcessFactory
{
    public IChildProcess Start(ChildLaunch child)
    {
        var process = Process.Start(ChildProcessStartInfoFactory.Create(child))
            ?? throw new InvalidOperationException($"Failed to start {child.Descriptor.Role}.");
        return new SystemChildProcess(child.Descriptor.Role, process);
    }
}

sealed class SystemChildProcess(ContainerRole role, Process process) : IChildProcess
{
    public ContainerRole Role { get; } = role;
    public int ProcessId => process.Id;
    public bool HasExited => process.HasExited;
    public int ExitCode => process.ExitCode;
    public Task WaitForExit(CancellationToken cancellationToken = default) => process.WaitForExitAsync(cancellationToken);
    public void KillTree() => process.Kill(entireProcessTree: true);
    public void Dispose() => process.Dispose();
}