namespace ServiceControl.Launcher;

using System.Collections;

static class Program
{
    const int ConfigurationErrorExitCode = 2;

    public static async Task<int> Main(string[] args)
    {
        try
        {
            var selection = RoleSelection.Parse(Environment.GetEnvironmentVariable("SERVICE_CONTROL_ROLE"));
            var command = ContainerCommand.Parse(args, selection.ProcessRoles.Count);
            var descriptors = LauncherEnvironment.CreateRoleDescriptors(
                Environment.GetEnvironmentVariable(LauncherEnvironment.RunInPlace),
                Environment.GetEnvironmentVariable(LauncherEnvironment.ApplicationRoot),
                AppContext.BaseDirectory);
            var plan = LaunchPlan.Create(selection, command, descriptors, ReadEnvironment());

            if (command.Mode == LauncherMode.Health)
            {
                Console.Error.WriteLine("Launcher health checks will be implemented with aggregate health support.");
                return ConfigurationErrorExitCode;
            }

            var shutdownTimeout = LauncherShutdownTimeout.Parse(
                Environment.GetEnvironmentVariable(LauncherShutdownTimeout.EnvironmentVariable));

            Console.WriteLine($"Selected process roles: {string.Join(", ", plan.Selection.ProcessRoles)}");
            Console.WriteLine($"Selected capabilities: {(plan.Selection.Capabilities.Count == 0 ? "none" : string.Join(", ", plan.Selection.Capabilities))}");

            using var shutdownSignals = new ShutdownSignalSource();
            var supervisor = new ChildProcessSupervisor(
                new SystemChildProcessFactory(),
                new UnixSignalSender(),
                TimeProvider.System);
            return await supervisor.Run(plan, shutdownSignals.Requested, shutdownTimeout).ConfigureAwait(false);
        }
        catch (LauncherConfigurationException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return ConfigurationErrorExitCode;
        }
    }

    static IReadOnlyDictionary<string, string?> ReadEnvironment()
    {
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry variable in Environment.GetEnvironmentVariables())
        {
            if (variable.Key is string key)
            {
                environment[key] = variable.Value?.ToString();
            }
        }

        return environment;
    }
}