namespace ServiceControlInstaller.Engine.Configuration.Monitoring
{
    using NuGet.Versioning;

    public static class SettingsList
    {
        public static readonly SettingInfo EndpointName = new()
        {
            Name = "Monitoring/EndpointName",
            RemovedFrom = new(5, 5, 0)
        };

        public static SettingInfo InstanceName = new()
        {
            Name = "Monitoring/InstanceName",
            SupportedFrom = new SemanticVersion(5, 5, 0)
        };

        public static SettingInfo Port = new() { Name = "Monitoring/HttpPort" };
        public static SettingInfo HostName = new() { Name = "Monitoring/HttpHostName" };
        public static SettingInfo LogPath = new() { Name = "Monitoring/LogPath" };
        public static SettingInfo TransportType = new() { Name = "Monitoring/TransportType" };
        public static SettingInfo ErrorQueue = new() { Name = "Monitoring/ErrorQueue" };
        public static SettingInfo ShutdownTimeout = new()
        {
            Name = "Monitoring/ShutdownTimeout",
            SupportedFrom = new SemanticVersion(6, 4, 1)
        };

        public static readonly SettingInfo HttpsEnabled = new()
        {
            Name = "Monitoring/Https.Enabled",
            SupportedFrom = new SemanticVersion(6, 9, 0)
        };

    }
}