namespace ServiceControl.ScaleOut
{
    using ServiceBus.Management.Infrastructure.Settings;

    public class ScaleOutGroupSettings
    {
        static bool ReconnectAutomaticallyDefault;
        static bool ConnectAutomaticallyDefault;
        static int MinimumConnectedDefault;

        static ScaleOutGroupSettings()
        {
            ReconnectAutomaticallyDefault = SettingsReader<bool>.Read("ScaleOutSettings", "ReconnectAutomatically", false);
            ConnectAutomaticallyDefault = SettingsReader<bool>.Read("ScaleOutSettings", "ConnectAutomatically", false);
            MinimumConnectedDefault = SettingsReader<int>.Read("ScaleOutSettings", "MinimumConnected", 1);
        }
        public ScaleOutGroupSettings(string id)
        {
            Id = id;
            ReconnectAutomatically = ReconnectAutomaticallyDefault;
            ConnectAutomatically = ConnectAutomaticallyDefault;
            MinimumConnected = MinimumConnectedDefault;
        }

        public string Id { get; set; }
        public bool ReconnectAutomatically { get; set; }
        public bool ConnectAutomatically { get; set; }
        public int MinimumConnected { get; set; }
    }
}