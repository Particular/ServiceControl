namespace ServiceControl.Configuration;

public readonly record struct SettingsRootNamespace(string Root)
{
    public override string ToString() => Root;
}