namespace ServiceControl.Launcher;

static class LauncherEnvironment
{
    public const string ApplicationRoot = "SERVICECONTROL_LAUNCHER_APP_ROOT";
    public const string RunInPlace = "SERVICECONTROL_LAUNCHER_RUN_IN_PLACE";

    public static IReadOnlyList<RoleDescriptor> CreateRoleDescriptors(
        string? runInPlaceValue,
        string? applicationRoot,
        string launcherBaseDirectory)
    {
        if (runInPlaceValue is null)
        {
            return RoleDescriptor.Create(applicationRoot ?? "/app");
        }

        if (!bool.TryParse(runInPlaceValue, out var runInPlace))
        {
            throw new LauncherConfigurationException($"{RunInPlace} must be 'true' or 'false'.");
        }

        return runInPlace
            ? RoleDescriptor.CreateDevelopment(launcherBaseDirectory, BuildConfiguration.Name)
            : RoleDescriptor.Create(applicationRoot ?? "/app");
    }

    static class BuildConfiguration
    {
#if DEBUG
        public const string Name = "Debug";
#else
        public const string Name = "Release";
#endif
    }
}
