namespace ServiceControl.Launcher;

using System.Collections;
using System.Diagnostics;

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

            return await Run(plan, CancellationToken.None).ConfigureAwait(false);
        }
        catch (LauncherConfigurationException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return ConfigurationErrorExitCode;
        }
    }

    static async Task<int> Run(LaunchPlan plan, CancellationToken cancellationToken)
    {
        foreach (var child in plan.Children)
        {
            if (!File.Exists(child.Descriptor.ExecutablePath))
            {
                throw new LauncherConfigurationException(
                    $"The {child.Descriptor.Role} executable was not found at '{child.Descriptor.ExecutablePath}'.");
            }
        }

        Console.WriteLine($"Selected process roles: {string.Join(", ", plan.Selection.ProcessRoles)}");
        Console.WriteLine($"Selected capabilities: {(plan.Selection.Capabilities.Count == 0 ? "none" : string.Join(", ", plan.Selection.Capabilities))}");

        var processes = new List<Process>();
        try
        {
            foreach (var child in plan.Children)
            {
                Console.WriteLine($"Starting {child.Descriptor.Role}: {child.Descriptor.ExecutablePath} (port {child.Descriptor.Port})");
                var startInfo = ChildProcessStartInfoFactory.Create(child);
                processes.Add(Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start {child.Descriptor.Role}."));
            }

            await Task.WhenAll(processes.Select(process => process.WaitForExitAsync(cancellationToken))).ConfigureAwait(false);
            return processes.Select(process => process.ExitCode).FirstOrDefault(exitCode => exitCode != 0);
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
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
