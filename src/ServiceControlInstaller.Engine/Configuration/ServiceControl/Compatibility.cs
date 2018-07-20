namespace ServiceControlInstaller.Engine.Configuration.ServiceControl
{
    using System;

    // For Removing and Adding Setting use SettingsList.cs

    public static class Compatibility
    {
        public static CompatibilityInfo ForwardingQueuesAreOptional = new CompatibilityInfo {SupportedFrom = new Version(1, 29)};

        public class CompatibilityInfo
        {
            public Version SupportedFrom { get; set; }
            public Version RemovedFrom { get; set; }
        }
    }
}