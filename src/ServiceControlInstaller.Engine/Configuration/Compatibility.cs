namespace ServiceControlInstaller.Engine.Configuration
{
    using System;

    // For Removing and Adding Setting use SettingsList.cs

    public static class Compatibility
    {
        public class CompatibilityInfo
        {
            public Version SupportedFrom { get; set; }
            public Version RemovedFrom { get; set; }
        }

        public static CompatibilityInfo ForwardingQueuesAreOptional = new CompatibilityInfo{SupportedFrom = new Version(1, 27)};
    }
}
