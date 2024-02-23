namespace Particular.ThroughputCollector.Configuration;

using ServiceControl.Configuration;

public class Settings
{
    // Service name is what the user chose when installing the instance or is passing on the command line.
    public Settings(string serviceName)
    {
        ServiceName = serviceName;

        TryLoadLicenseFromConfig();
    }

    public static bool ValidateConfiguration => SettingsReader.Read(SettingsRootNamespace, "ValidateConfig", true);

    public string RootUrl
    {
        get
        {
            var suffix = string.Empty;

            if (!string.IsNullOrEmpty(VirtualDirectory))
            {
                suffix = $"{VirtualDirectory}/";
            }

            return $"http://{Hostname}:{Port}/{suffix}";
        }
    }

    public string ApiUrl => $"{RootUrl}api";

    public int Port { get; set; }

    public static string Hostname => SettingsReader.Read(SettingsRootNamespace, "Hostname", "localhost");
    public static string VirtualDirectory => SettingsReader.Read(SettingsRootNamespace, "VirtualDirectory", string.Empty);

    public string? LicenseFileText { get; set; }

    public string ServiceName { get; }

    void TryLoadLicenseFromConfig() => LicenseFileText = SettingsReader.Read<string>(SettingsRootNamespace, "LicenseText");

    public const string DefaultServiceName = "Particular.ThroughputCollection";
    public static readonly SettingsRootNamespace SettingsRootNamespace = new("ThroughputCollection");
}
