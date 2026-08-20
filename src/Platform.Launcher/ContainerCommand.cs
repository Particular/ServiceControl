namespace ServiceControl.Launcher;

enum LauncherMode
{
    Run,
    Health
}

sealed class ContainerCommand
{
    ContainerCommand(LauncherMode mode, IReadOnlyList<string> childArguments)
    {
        Mode = mode;
        ChildArguments = childArguments;
    }

    public LauncherMode Mode { get; }
    public IReadOnlyList<string> ChildArguments { get; }

    public static ContainerCommand Parse(IReadOnlyList<string> arguments, int processRoleCount)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentOutOfRangeException.ThrowIfLessThan(processRoleCount, 1);

        var mode = LauncherMode.Run;
        var childArgumentOffset = 0;

        if (arguments.Count > 0 && arguments[0].Equals("run", StringComparison.OrdinalIgnoreCase))
        {
            childArgumentOffset = 1;
        }
        else if (arguments.Count > 0 && arguments[0].Equals("health", StringComparison.OrdinalIgnoreCase))
        {
            mode = LauncherMode.Health;
            childArgumentOffset = 1;
        }

        var childArguments = arguments.Skip(childArgumentOffset).ToArray();

        if (mode == LauncherMode.Health && childArguments.Length > 0)
        {
            throw new LauncherConfigurationException("The health command does not accept application arguments.");
        }

        if (mode == LauncherMode.Run && processRoleCount > 1 &&
            !(childArguments.Length == 0 || childArguments is ["--setup-and-run"]))
        {
            throw new LauncherConfigurationException(
                "Multiple process roles support only normal run or exactly '--setup-and-run'. Select one process role to use maintenance, import, help, setup, or other application commands.");
        }

        return new ContainerCommand(mode, childArguments);
    }
}
