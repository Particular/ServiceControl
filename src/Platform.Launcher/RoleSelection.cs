namespace ServiceControl.Launcher;

sealed class RoleSelection
{
    const string AllowedValues = "Primary, Audit, Monitoring, ServicePulse, All";

    RoleSelection(IReadOnlyList<ContainerRole> processRoles, IReadOnlyList<ContainerCapability> capabilities)
    {
        ProcessRoles = processRoles;
        Capabilities = capabilities;
    }

    public IReadOnlyList<ContainerRole> ProcessRoles { get; }
    public IReadOnlyList<ContainerCapability> Capabilities { get; }

    public bool HasCapability(ContainerCapability capability) => Capabilities.Contains(capability);

    public static RoleSelection Parse(string? value)
    {
        if (value is null)
        {
            return new RoleSelection([ContainerRole.Primary], []);
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw InvalidSelection("The value cannot be empty.");
        }

        var requestedRoles = new HashSet<ContainerRole>();
        var capabilities = new HashSet<ContainerCapability>();

        foreach (var element in value.Split(','))
        {
            var role = element.Trim();
            if (role.Length == 0)
            {
                throw InvalidSelection("Empty role elements are not allowed.");
            }

            if (role.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                requestedRoles.UnionWith(Enum.GetValues<ContainerRole>());
                capabilities.Add(ContainerCapability.ServicePulse);
            }
            else if (role.Equals("ServicePulse", StringComparison.OrdinalIgnoreCase))
            {
                requestedRoles.Add(ContainerRole.Primary);
                capabilities.Add(ContainerCapability.ServicePulse);
            }
            else if (Enum.GetNames<ContainerRole>().FirstOrDefault(name => name.Equals(role, StringComparison.OrdinalIgnoreCase)) is { } roleName)
            {
                requestedRoles.Add(Enum.Parse<ContainerRole>(roleName));
            }
            else
            {
                throw InvalidSelection($"Unknown role '{role}'.");
            }
        }

        var orderedRoles = Enum.GetValues<ContainerRole>().Where(requestedRoles.Contains).ToArray();
        var orderedCapabilities = Enum.GetValues<ContainerCapability>().Where(capabilities.Contains).ToArray();
        return new RoleSelection(orderedRoles, orderedCapabilities);
    }

    static LauncherConfigurationException InvalidSelection(string reason) =>
        new($"Invalid SERVICE_CONTROL_ROLE. {reason} Allowed values: {AllowedValues}.");
}
