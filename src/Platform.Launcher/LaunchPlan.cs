namespace ServiceControl.Launcher;

sealed record ChildLaunch(
    RoleDescriptor Descriptor,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> EnvironmentOverrides);

sealed class LaunchPlan
{
    const string IntegratedServicePulseVariable = "SERVICECONTROL_ENABLEINTEGRATEDSERVICEPULSE";

    LaunchPlan(RoleSelection selection, ContainerCommand command, IReadOnlyList<ChildLaunch> children)
    {
        Selection = selection;
        Command = command;
        Children = children;
    }

    public RoleSelection Selection { get; }
    public ContainerCommand Command { get; }
    public IReadOnlyList<ChildLaunch> Children { get; }

    public static LaunchPlan Create(
        RoleSelection selection,
        ContainerCommand command,
        IEnumerable<RoleDescriptor> descriptors,
        IReadOnlyDictionary<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(descriptors);
        ArgumentNullException.ThrowIfNull(environment);

        var descriptorsByRole = descriptors.ToDictionary(descriptor => descriptor.Role);
        var servicePulseSelected = selection.HasCapability(ContainerCapability.ServicePulse);
        var integratedServicePulseValue = FindEnvironmentValue(environment, IntegratedServicePulseVariable);

        if (servicePulseSelected && bool.TryParse(integratedServicePulseValue, out var enabled) && !enabled)
        {
            throw new LauncherConfigurationException(
                $"The ServicePulse capability requires {IntegratedServicePulseVariable}=true, but it is explicitly disabled.");
        }

        var children = selection.ProcessRoles.Select(role =>
        {
            if (!descriptorsByRole.TryGetValue(role, out var descriptor))
            {
                throw new LauncherConfigurationException($"No launcher descriptor is configured for the {role} role.");
            }

            IReadOnlyDictionary<string, string> overrides = new Dictionary<string, string>();
            if (role == ContainerRole.Primary && servicePulseSelected && integratedServicePulseValue is null)
            {
                overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [IntegratedServicePulseVariable] = bool.TrueString.ToLowerInvariant()
                };
            }

            return new ChildLaunch(descriptor, command.ChildArguments.ToArray(), overrides);
        }).ToArray();

        return new LaunchPlan(selection, command, children);
    }

    static string? FindEnvironmentValue(IReadOnlyDictionary<string, string?> environment, string name)
    {
        foreach (var pair in environment)
        {
            if (pair.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }
}
