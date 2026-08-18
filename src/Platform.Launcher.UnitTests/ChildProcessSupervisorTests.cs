namespace ServiceControl.Launcher.UnitTests;

using NUnit.Framework;

[TestFixture]
public class ChildProcessSupervisorTests
{
    string applicationRoot = null!;

    [SetUp]
    public void SetUp()
    {
        applicationRoot = Path.Combine(Path.GetTempPath(), $"launcher-tests-{Guid.NewGuid():N}");
        foreach (var descriptor in RoleDescriptor.Create(applicationRoot))
        {
            Directory.CreateDirectory(descriptor.WorkingDirectory);
            File.WriteAllText(descriptor.ExecutablePath, string.Empty);
        }
    }

    [TearDown]
    public void TearDown() => Directory.Delete(applicationRoot, true);

    [Test]
    public async Task Children_start_in_canonical_order_and_an_exit_stops_siblings()
    {
        var factory = new FakeProcessFactory();
        var signals = new FakeSignalSender(stopOnSignal: true);
        var supervisor = CreateSupervisor(factory, signals);
        var run = supervisor.Run(CreatePlan("Monitoring,Primary,Audit"), NeverShutdown(CancellationToken.None), TimeSpan.FromSeconds(1));

        factory.Processes[1].Exit(42);
        var exitCode = await run.ConfigureAwait(false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(factory.Processes.Select(process => process.Role), Is.EqualTo(Enum.GetValues<ContainerRole>()));
            Assert.That(signals.Sent.Select(sent => sent.Role), Is.EqualTo(new[] { ContainerRole.Primary, ContainerRole.Monitoring }));
            Assert.That(signals.Sent.Select(sent => sent.Signal), Has.All.EqualTo(ShutdownSignal.Terminate));
            Assert.That(exitCode, Is.EqualTo(42));
            Assert.That(factory.Processes, Has.All.Property("Disposed").True);
        }
    }

    [Test]
    public async Task Requested_signal_is_forwarded_to_every_running_child()
    {
        var factory = new FakeProcessFactory();
        var signals = new FakeSignalSender(stopOnSignal: true);
        var shutdown = new TaskCompletionSource<ShutdownSignal>(TaskCreationOptions.RunContinuationsAsynchronously);
        var run = CreateSupervisor(factory, signals)
            .Run(CreatePlan("Primary,Audit"), shutdown.Task, TimeSpan.FromSeconds(1));

        shutdown.SetResult(ShutdownSignal.Interrupt);
        var exitCode = await run.ConfigureAwait(false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(signals.Sent.Select(sent => sent.Role), Is.EqualTo(new[] { ContainerRole.Primary, ContainerRole.Audit }));
            Assert.That(signals.Sent.Select(sent => sent.Signal), Has.All.EqualTo(ShutdownSignal.Interrupt));
            Assert.That(exitCode, Is.Zero);
        }
    }

    [Test]
    public async Task Zero_exit_is_a_failure_when_sibling_roles_were_expected_to_keep_running()
    {
        var factory = new FakeProcessFactory();
        var signals = new FakeSignalSender(stopOnSignal: true);
        var run = CreateSupervisor(factory, signals)
            .Run(CreatePlan("Primary,Audit"), NeverShutdown(CancellationToken.None), TimeSpan.FromSeconds(1));

        factory.Processes[0].Exit(0);

        Assert.That(await run.ConfigureAwait(false), Is.EqualTo(ChildProcessSupervisor.LauncherFailureExitCode));
    }

    [Test]
    public async Task Single_role_zero_exit_is_preserved_for_maintenance_commands()
    {
        var factory = new FakeProcessFactory();
        var run = CreateSupervisor(factory, new FakeSignalSender(stopOnSignal: true))
            .Run(CreatePlan("Primary"), NeverShutdown(CancellationToken.None), TimeSpan.FromSeconds(1));

        factory.Processes[0].Exit(0);

        Assert.That(await run.ConfigureAwait(false), Is.Zero);
    }

    [Test]
    public async Task Children_still_running_after_the_grace_period_have_their_process_trees_killed()
    {
        var factory = new FakeProcessFactory();
        var signals = new FakeSignalSender(stopOnSignal: false);
        var shutdown = Task.FromResult(ShutdownSignal.Terminate);

        var exitCode = await CreateSupervisor(factory, signals)
            .Run(CreatePlan("Primary,Audit"), shutdown, TimeSpan.FromMilliseconds(10))
            .ConfigureAwait(false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exitCode, Is.Zero);
            Assert.That(factory.Processes, Has.All.Property("TreeKilled").True);
            Assert.That(factory.Processes, Has.All.Property("HasExited").True);
        }
    }

    [Test]
    public void Every_executable_is_verified_before_any_child_starts()
    {
        File.Delete(RoleDescriptor.Create(applicationRoot)[1].ExecutablePath);
        var factory = new FakeProcessFactory();

        var exception = Assert.ThrowsAsync<LauncherConfigurationException>(() => CreateSupervisor(factory, new FakeSignalSender(true))
            .Run(CreatePlan("Primary,Audit"), NeverShutdown(CancellationToken.None), TimeSpan.FromSeconds(1)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception!.Message, Does.Contain("Audit executable was not found"));
            Assert.That(factory.Processes, Is.Empty);
        }
    }

    LaunchPlan CreatePlan(string roles)
    {
        var selection = RoleSelection.Parse(roles);
        var command = ContainerCommand.Parse([], selection.ProcessRoles.Count);
        return LaunchPlan.Create(selection, command, RoleDescriptor.Create(applicationRoot), new Dictionary<string, string?>());
    }

    static ChildProcessSupervisor CreateSupervisor(FakeProcessFactory factory, FakeSignalSender signals) =>
        new(factory, signals, TimeProvider.System);

    static Task<ShutdownSignal> NeverShutdown(CancellationToken cancellationToken) =>
        Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ContinueWith(
            _ => ShutdownSignal.Terminate,
            cancellationToken,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

    sealed class FakeProcessFactory : IChildProcessFactory
    {
        public List<FakeProcess> Processes { get; } = [];

        public IChildProcess Start(ChildLaunch child)
        {
            var process = new FakeProcess(child.Descriptor.Role, Processes.Count + 100);
            Processes.Add(process);
            return process;
        }
    }

    sealed class FakeProcess(ContainerRole role, int processId) : IChildProcess
    {
        readonly TaskCompletionSource exited = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ContainerRole Role { get; } = role;
        public int ProcessId { get; } = processId;
        public bool HasExited => exited.Task.IsCompleted;
        public int ExitCode { get; private set; }
        public bool TreeKilled { get; private set; }
        public bool Disposed { get; private set; }

        public Task WaitForExit(CancellationToken cancellationToken = default) => exited.Task.WaitAsync(cancellationToken);

        public void Exit(int code)
        {
            ExitCode = code;
            exited.TrySetResult();
        }

        public void KillTree()
        {
            TreeKilled = true;
            Exit(137);
        }

        public void Dispose() => Disposed = true;
    }

    sealed class FakeSignalSender(bool stopOnSignal) : IChildSignalSender
    {
        public List<(ContainerRole Role, ShutdownSignal Signal)> Sent { get; } = [];

        public void Send(IChildProcess child, ShutdownSignal signal)
        {
            Sent.Add((child.Role, signal));
            if (stopOnSignal)
            {
                ((FakeProcess)child).Exit(0);
            }
        }
    }
}