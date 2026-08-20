namespace ServiceControl.Launcher;

sealed record RoleDescriptor(
    ContainerRole Role,
    string ExecutablePath,
    string WorkingDirectory,
    Uri HealthEndpoint,
    int Port)
{
    public static IReadOnlyList<RoleDescriptor> Create(string applicationRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationRoot);

        return
        [
            Create(applicationRoot, ContainerRole.Primary, "primary", "ServiceControl", "http://localhost:33333/api/configuration", 33333),
            Create(applicationRoot, ContainerRole.Audit, "audit", "ServiceControl.Audit", "http://localhost:44444/api/configuration", 44444),
            Create(applicationRoot, ContainerRole.Monitoring, "monitoring", "ServiceControl.Monitoring", "http://localhost:33633/connection", 33633)
        ];
    }

    public static IReadOnlyList<RoleDescriptor> CreateDevelopment(string launcherBaseDirectory, string configuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(launcherBaseDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration);

        // Keep each child in its project output directory. The transport and persistence plugin
        // loaders use their assembly locations to find development manifests in sibling projects.
        var sourceRoot = Path.GetFullPath(Path.Combine(launcherBaseDirectory, "..", "..", "..", ".."));
        var executableSuffix = OperatingSystem.IsWindows() ? ".exe" : string.Empty;

        return
        [
            CreateDevelopment(sourceRoot, configuration, ContainerRole.Primary, "ServiceControl", $"ServiceControl{executableSuffix}", "http://localhost:33333/api/configuration", 33333),
            CreateDevelopment(sourceRoot, configuration, ContainerRole.Audit, "ServiceControl.Audit", $"ServiceControl.Audit{executableSuffix}", "http://localhost:44444/api/configuration", 44444),
            CreateDevelopment(sourceRoot, configuration, ContainerRole.Monitoring, "ServiceControl.Monitoring", $"ServiceControl.Monitoring{executableSuffix}", "http://localhost:33633/connection", 33633)
        ];
    }

    static RoleDescriptor Create(string root, ContainerRole role, string directory, string executable, string healthEndpoint, int port)
    {
        var workingDirectory = Path.Combine(root, directory);
        return new(role, Path.Combine(workingDirectory, executable), workingDirectory, new Uri(healthEndpoint), port);
    }

    static RoleDescriptor CreateDevelopment(string sourceRoot, string configuration, ContainerRole role, string project, string executable, string healthEndpoint, int port)
    {
        var workingDirectory = Path.Combine(sourceRoot, project, "bin", configuration, "net10.0");
        return new(role, Path.Combine(workingDirectory, executable), workingDirectory, new Uri(healthEndpoint), port);
    }
}
